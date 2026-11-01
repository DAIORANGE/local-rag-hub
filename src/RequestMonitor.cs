using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;

namespace LocalRagHub {
 public sealed class RequestRecord {
   public int Id, Status, RequestBytes, ResponseBytes;
   public long Milliseconds;
   public DateTime When;
   public string Stage, Source, Group, Path, Method, RequestPreview, ResponsePreview, Error, UserAgent;
   public bool ExplicitSource, ExplicitTrace;
   public string Summary {
     get {return "[REQ #"+Id.ToString("0000")+"] "+Stage+" | "+Source+(ExplicitSource?"":" (?)")+
       " | "+Group+" | "+Status+" | "+Milliseconds+"ms | "+RequestBytes+"B";}
   }
   public string Detail {
     get {
       return "编号 #"+Id+"   "+When.ToString("yyyy-MM-dd HH:mm:ss.fff")+
         "\r\n服务："+Stage+"\r\n来源："+Source+(ExplicitSource?"（客户端标识）":"（未知，不证明软件/插件）")+
         "\r\n关联："+Group+(ExplicitTrace?"（共同 X-RAG-Trace-ID）":"（未核实；仅邻近推测）")+
         "\r\n请求："+Method+" "+Path+"\r\n状态："+Status+"    耗时："+Milliseconds+"ms"+
         "\r\n大小："+RequestBytes+"B 请求 / "+ResponseBytes+"B 响应"+
         "\r\nUser-Agent 参考："+(String.IsNullOrWhiteSpace(UserAgent)?"未提供":UserAgent)+
         "\r\n错误："+(String.IsNullOrWhiteSpace(Error)?"无":Error)+
         "\r\n\r\n请求预览（仅内存，最多 700 字符，可能包含私密剧情）：\r\n"+RequestPreview+
         "\r\n\r\n响应预览：\r\n"+ResponsePreview+
         "\r\n\r\n注：无法从普通 llama-server 日志证明多个请求属于同一轮 RP。";
     }
   }
 }
 public sealed class RequestMonitor:IDisposable {
   public const int EmbeddingPort=12435,RerankerPort=12436,QueryPort=12437;
   readonly HubConfig config;readonly Action<RequestRecord> callback;
   readonly object gate=new object();readonly List<HttpListener> servers=new List<HttpListener>();
   readonly List<RequestRecord> history=new List<RequestRecord>();
   readonly Dictionary<string,Tuple<string,DateTime>> recent=new Dictionary<string,Tuple<string,DateTime>>();
   int nextId, nextGroup;
   public RequestMonitor(HubConfig cfg,Action<RequestRecord> onRecord){config=Paths.Clone(cfg);callback=onRecord;}
   public bool Running{get{lock(gate)return servers.Count==3;}}
   public RequestRecord[] Snapshot(){lock(gate)return history.ToArray();}
   public RequestRecord Find(int id){lock(gate)return history.FirstOrDefault(x=>x.Id==id);}
   public static string SafeTag(string s,int max=48){
     s=Regex.Replace(s??"", @"[^\p{L}\p{N}_.\-]","_").Trim('_');
     return s.Length>max?s.Substring(0,max):s;
   }
   public void Start(){
     if(Running)return;
     int[] capture={EmbeddingPort,RerankerPort,QueryPort};
     int[] targets={config.EmbeddingPort,config.RerankerPort,config.QueryPort};
     if(targets.Any(x=>capture.Contains(x)))throw new InvalidOperationException("监控端口与模型端口冲突");
     var list=new List<HttpListener>();
     try{
       for(int i=0;i<3;i++){
         var l=new HttpListener();l.Prefixes.Add("http://127.0.0.1:"+capture[i]+"/");l.Start();list.Add(l);
       }
       lock(gate)servers.AddRange(list);
       string[] roles={"Embedding","Reranker","Query"};
       for(int i=0;i<3;i++){var l=list[i];var role=roles[i];int target=targets[i];Task.Run(()=>Accept(l,role,target));}
     }catch{
       foreach(var l in list){try{l.Close();}catch{}}
       lock(gate)servers.Clear();throw;
     }
   }
   public void Stop(){
     HttpListener[] close;lock(gate){close=servers.ToArray();servers.Clear();}
     foreach(var s in close){try{s.Close();}catch{}}
   }
   public void Dispose(){Stop();}
   void Accept(HttpListener listener,string role,int targetPort){
     while(listener.IsListening){
       HttpListenerContext ctx;
       try{ctx=listener.GetContext();}
       catch(HttpListenerException){break;}catch(ObjectDisposedException){break;}catch{break;}
       var accepted=ctx;Task.Run(()=>Relay(accepted,role,targetPort));
     }
   }
   string Group(string source,bool explicitSource,string trace,DateTime time,out bool proven){
     if(!String.IsNullOrEmpty(trace)){proven=true;return "RP:"+trace;}
     proven=false;
     if(!explicitSource)return "未关联";
     lock(gate){
       Tuple<string,DateTime> old;
       if(recent.TryGetValue(source,out old)&&(time-old.Item2).TotalSeconds<=25){
         recent[source]=Tuple.Create(old.Item1,time);return old.Item1;
       }
       string tag="≈候选"+(++nextGroup).ToString("000");
       recent[source]=Tuple.Create(tag,time);return tag;
     }
   }
   static byte[] ReadCapped(Stream stream){
     using(var data=new MemoryStream()){
       byte[] block=new byte[8192];int count;
       while((count=stream.Read(block,0,block.Length))>0){
         if(data.Length+count>16*1024*1024)throw new InvalidDataException("监控入口请求超过 16 MiB");
         data.Write(block,0,count);
       }
       return data.ToArray();
     }
   }
   static string Brief(byte[] b,int max=700){
     if(b==null||b.Length==0)return "（空）";
     string s=Encoding.UTF8.GetString(b,0,Math.Min(b.Length,4096));
     s=Regex.Replace(s,"(?i)(\\\"(?:api[_-]?key|authorization|password|secret|access_token|refresh_token)\\\"\\s*:\\s*\\\")[^\\\"]*(\\\")","$1[REDACTED]$2");
     s=s.Replace("\r"," ").Replace("\n"," ");
     return s.Length>max?s.Substring(0,max)+"…（已截断）":s;
   }
   void Relay(HttpListenerContext ctx,string stage,int targetPort){
     DateTime began=DateTime.Now;var sw=Stopwatch.StartNew();
     var inc=ctx.Request;int status=502,rx=0,tx=0;string err="",preview="（空）",result="（空）";
     string path=inc.Url.AbsolutePath,source=SafeTag(inc.Headers["X-RAG-Source"]);
     bool hasSource=!String.IsNullOrEmpty(source);
     var tagged=Regex.Match(path,@"^/src/([^/]+)(/.*)$",RegexOptions.IgnoreCase);
     if(tagged.Success){
       source=SafeTag(Uri.UnescapeDataString(tagged.Groups[1].Value));
       hasSource=!String.IsNullOrWhiteSpace(source);path=tagged.Groups[2].Value;
     }
     if(!hasSource)source="来源未知";
     bool traced=false;string group=Group(source,hasSource,SafeTag(inc.Headers["X-RAG-Trace-ID"]),began,out traced);
     try{
       if(!(path.StartsWith("/v1/",StringComparison.OrdinalIgnoreCase)||path=="/health"||path=="/rerank")){
         status=404;throw new InvalidDataException("不允许转发此路径");
       }
       byte[] data=ReadCapped(inc.InputStream);rx=data.Length;preview=Brief(data);
       var req=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:"+targetPort+path+inc.Url.Query);
       req.Proxy=null;req.Method=inc.HttpMethod;req.Timeout=120000;req.ReadWriteTimeout=120000;
       req.AllowAutoRedirect=false;req.KeepAlive=false;
       if(!String.IsNullOrWhiteSpace(inc.ContentType))req.ContentType=inc.ContentType;
       if(!String.IsNullOrWhiteSpace(inc.UserAgent))req.UserAgent=inc.UserAgent;
       if(!String.IsNullOrWhiteSpace(inc.Headers["Authorization"]))req.Headers[HttpRequestHeader.Authorization]=inc.Headers["Authorization"];
       if(data.Length>0){req.ContentLength=data.Length;using(var body=req.GetRequestStream())body.Write(data,0,data.Length);}
       else if(inc.HasEntityBody)req.ContentLength=0;
       HttpWebResponse resp;
       try{resp=(HttpWebResponse)req.GetResponse();}
       catch(WebException e){resp=e.Response as HttpWebResponse;if(resp==null)throw;}
       using(resp){
         status=(int)resp.StatusCode;ctx.Response.StatusCode=status;
         ctx.Response.ContentType=resp.ContentType??"application/json";ctx.Response.SendChunked=true;
         using(var upstream=resp.GetResponseStream())
         using(var sample=new MemoryStream()){
           byte[] block=new byte[8192];int n;
           while(upstream!=null&&(n=upstream.Read(block,0,block.Length))>0){
             ctx.Response.OutputStream.Write(block,0,n);ctx.Response.OutputStream.Flush();
             tx+=n;if(sample.Length<2048)sample.Write(block,0,Math.Min(n,(int)(2048-sample.Length)));
           }
           result=Brief(sample.ToArray(),480);
         }
       }
     }catch(Exception ex){
       err=ex.GetBaseException().Message;
       if(status==502 && ex is InvalidDataException)status=413;
       try{
         ctx.Response.StatusCode=status;ctx.Response.ContentType="application/json";
         byte[] data=Encoding.UTF8.GetBytes("{\"error\":\"local request relay failed\"}");
         ctx.Response.OutputStream.Write(data,0,data.Length);
       }catch{}
     }finally{
       sw.Stop();
       var record=new RequestRecord{Id=Interlocked.Increment(ref nextId),When=began,Stage=stage,
         Source=source,ExplicitSource=hasSource,Group=group,ExplicitTrace=traced,
         Method=inc.HttpMethod,Path=path,Status=status,Milliseconds=sw.ElapsedMilliseconds,
         RequestBytes=rx,ResponseBytes=tx,RequestPreview=preview,ResponsePreview=result,
         Error=err,UserAgent=SafeTag(inc.UserAgent,90)};
       lock(gate){history.Add(record);if(history.Count>200)history.RemoveAt(0);}
       try{if(callback!=null)callback(record);}catch{}
       try{ctx.Response.Close();}catch{}
     }
   }
 }
 public sealed class RequestDetailDialog:Form {
   public RequestDetailDialog(RequestRecord r){
     Text="请求 #"+r.Id+" · "+r.Stage;ClientSize=new Size(820,620);
     BackColor=Theme.Bg;ForeColor=Color.White;StartPosition=FormStartPosition.CenterParent;
     Controls.Add(new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,
       WordWrap=true,Location=new Point(15,15),Size=new Size(790,585),Text=r.Detail,
       BackColor=Theme.Card,ForeColor=Color.White,Font=new Font("Consolas",10)});
   }
 }
 public sealed class RequestListDialog:Form {
   public RequestListDialog(RequestMonitor monitor){
     Text="最近请求 · 最多 200 条 · 只保留在内存";ClientSize=new Size(1010,575);
     StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Bg;ForeColor=Color.White;
     Theme.Label(this,"双击查看预览。精确关联需要客户端发送相同的 X-RAG-Trace-ID。",15,9,980,27,10,Theme.Cyan);
     var view=new ListView{Location=new Point(15,40),Size=new Size(980,370),View=View.Details,FullRowSelect=true,
       BackColor=Theme.Card,ForeColor=Color.White,Font=Theme.Font(9)};
     view.Columns.Add("时间",120);view.Columns.Add("来源",170);view.Columns.Add("模型服务",125);
     view.Columns.Add("归组",140);view.Columns.Add("状态",75);view.Columns.Add("耗时",80);view.Columns.Add("请求大小",115);
     foreach(var r in monitor.Snapshot().Reverse()){
       var item=new ListViewItem(r.When.ToString("HH:mm:ss.fff"));item.Tag=r;
       item.SubItems.Add(r.Source+(r.ExplicitSource?"":" (?)"));item.SubItems.Add(r.Stage);
       item.SubItems.Add(r.Group);item.SubItems.Add(r.Status.ToString());item.SubItems.Add(r.Milliseconds+"ms");
       item.SubItems.Add(r.RequestBytes+"B");view.Items.Add(item);
     }
     view.DoubleClick+=(s,e)=>{if(view.SelectedItems.Count>0){
       var selected=view.SelectedItems[0].Tag as RequestRecord;
       if(selected!=null)using(var d=new RequestDetailDialog(selected))d.ShowDialog(this);
     }};Controls.Add(view);
     Theme.Label(this,"只有改用记录入口的调用方才会出现；原服务端口仍旧直连且不受影响。",16,420,980,26,10,Theme.Yellow);
     Controls.Add(new TextBox{Multiline=true,ReadOnly=true,Location=new Point(15,450),Size=new Size(965,105),
       BackColor=Theme.Card,ForeColor=Color.White,Font=new Font("Consolas",10),
       Text="Embedding: http://127.0.0.1:12435/src/TT-Nailong/v1\r\n"+
            "Reranker:  http://127.0.0.1:12436/src/TT-Nailong/v1\r\n"+
            "Query:     http://127.0.0.1:12437/src/TT-Nailong/v1\r\n"+
            "把 TT-Nailong 换成你的插件标签。关闭面板将停止这些代理入口。"});
   }
 }
}