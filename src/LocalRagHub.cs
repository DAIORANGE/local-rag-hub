using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Management;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace LocalRagHub {
 public class HubConfig {
   public string ServerPath="", EmbeddingPath="", RerankerPath="", EmbeddingId="Qwen3-Embedding-8B", RerankerId="Qwen3-Reranker-8B", EmbeddingPooling="last";
   public bool QueryEnabled=false, QueryDisableThinking=true;
   public string QueryPath="", QueryId="Qwen3.5-9B";
   public int QueryPort=11437, QueryContext=8192, QueryParallel=1, QueryGpuLayers=99;
   public int EmbeddingPort=11435, RerankerPort=11436, Context=16384, EmbeddingParallel=4, RerankerParallel=8, GpuLayers=99;
 }
 public class Unit {
   public string Role, File, Alias; public int Port, Parallel;
   public Unit(string role,string file,string alias,int port,int parallel){Role=role;File=file;Alias=alias;Port=port;Parallel=parallel;}
 }
 public class UnitStatus {
   public string State="STOPPED",Detail="";public int Pid=0; public DateTime Since=DateTime.MinValue;
 }
 public static class Paths {
   public static readonly string AppDir=AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
   public static readonly string ConfigFile=Path.Combine(AppDir,"config.json");
   public static readonly string LogDir=Path.Combine(AppDir,"logs");
   public static readonly string UiLog=Path.Combine(LogDir,"hub.log");
   public static string Resolve(string path){
      if(String.IsNullOrWhiteSpace(path))return "";
      return Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine(AppDir,path));
   }
   static string Pick(params string[] candidates){
      foreach(var p in candidates){if(!String.IsNullOrEmpty(p)&&File.Exists(p))return p;}
      return candidates[0];
   }
   public static HubConfig Defaults(){
      var root=Path.GetFullPath(Path.Combine(AppDir,".."));
      var d=new HubConfig();
      d.ServerPath=Pick(Path.Combine(AppDir,@"llama.cpp\llama-server.exe"),Path.Combine(root,@"llama.cpp\llama-server.exe"));
      d.EmbeddingPath=Pick(Path.Combine(AppDir,@"models\embedding\Qwen3-Embedding-8B-Q4_K_M.gguf"),Path.Combine(root,@"models\embedding\Qwen3-Embedding-8B-Q4_K_M.gguf"));
      d.RerankerPath=Pick(Path.Combine(AppDir,@"models\reranker\Qwen3-Reranker-8B-Q4_K_M.gguf"),Path.Combine(root,@"models\reranker\Qwen3-Reranker-8B-Q4_K_M.gguf"));
      d.QueryPath=Pick(Path.Combine(AppDir,@"models\query\Qwen_Qwen3.5-9B-Q5_K_M.gguf"),Path.Combine(root,@"models\query\Qwen_Qwen3.5-9B-Q5_K_M.gguf"));
      return d;
   }
   public static List<string> Missing(HubConfig c){
      var m=new List<string>();
      if(String.IsNullOrWhiteSpace(c.ServerPath)||!File.Exists(Resolve(c.ServerPath)))m.Add("llama-server.exe");
      if(String.IsNullOrWhiteSpace(c.EmbeddingPath)||!File.Exists(Resolve(c.EmbeddingPath)))m.Add("Embedding 权重");
      if(String.IsNullOrWhiteSpace(c.RerankerPath)||!File.Exists(Resolve(c.RerankerPath)))m.Add("Reranker 权重");
      if(c.QueryEnabled && (String.IsNullOrWhiteSpace(c.QueryPath)||!File.Exists(Resolve(c.QueryPath))))m.Add("Query 权重");
      return m;
   }
   public static HubConfig Read(){
     try {
       if(File.Exists(ConfigFile)){
         var c=new JavaScriptSerializer().Deserialize<HubConfig>(File.ReadAllText(ConfigFile,Encoding.UTF8));
         if(c!=null)return c;
       }
     } catch(Exception ex){File.AppendAllText(Path.Combine(AppDir,"config-error.log"),ex.Message+Environment.NewLine);}
     return Defaults();
   }
   public static HubConfig Clone(HubConfig c){return new JavaScriptSerializer().Deserialize<HubConfig>(new JavaScriptSerializer().Serialize(c));}
   public static void Save(HubConfig c){
     Validate(c);
     var json=new JavaScriptSerializer().Serialize(c);
     var temp=ConfigFile+".tmp";
     File.WriteAllText(temp,json,Encoding.UTF8);
     if(File.Exists(ConfigFile)) File.Replace(temp,ConfigFile,ConfigFile+".bak",true);
     else File.Move(temp,ConfigFile);
   }
   public static void Validate(HubConfig c) {
     if(c.EmbeddingPort<1024||c.EmbeddingPort>65535||c.RerankerPort<1024||c.RerankerPort>65535||c.EmbeddingPort==c.RerankerPort)throw new Exception("两个服务须使用不同的有效端口 (1024-65535)");
     if(String.IsNullOrWhiteSpace(c.ServerPath)||String.IsNullOrWhiteSpace(c.EmbeddingPath)||String.IsNullOrWhiteSpace(c.RerankerPath))throw new Exception("三个程序/模型文件路径不能为空");
     if(!File.Exists(Resolve(c.ServerPath)))throw new Exception("找不到 llama-server.exe："+Resolve(c.ServerPath));
     if(!File.Exists(Resolve(c.EmbeddingPath)))throw new Exception("找不到 Embedding 权重："+Resolve(c.EmbeddingPath));
     if(!File.Exists(Resolve(c.RerankerPath)))throw new Exception("找不到 Reranker 权重："+Resolve(c.RerankerPath));
     if(c.Context<512||c.Context>131072||c.GpuLayers<0||c.GpuLayers>999||c.EmbeddingParallel<1||c.EmbeddingParallel>32||c.RerankerParallel<1||c.RerankerParallel>32)throw new Exception("上下文/并发/GPU层数超出允许范围");
     if(String.IsNullOrWhiteSpace(c.EmbeddingId)||String.IsNullOrWhiteSpace(c.RerankerId))throw new Exception("模型 ID 不能为空");
     if(new[]{"last","mean","cls","none"}.All(x=>x!=c.EmbeddingPooling))throw new Exception("Embedding pooling 只能是 last / mean / cls / none");
      if(c.QueryEnabled){
        if(c.QueryPort<1024||c.QueryPort>65535||c.QueryPort==c.EmbeddingPort||c.QueryPort==c.RerankerPort)throw new Exception("Query 端口无效或与已有服务冲突");
        if(String.IsNullOrWhiteSpace(c.QueryPath)||!File.Exists(Resolve(c.QueryPath)))throw new Exception("找不到 Query Chat GGUF："+Resolve(c.QueryPath));
        if(String.IsNullOrWhiteSpace(c.QueryId))throw new Exception("Query 模型 ID 不能为空");
        if(c.QueryContext<512||c.QueryContext>131072||c.QueryParallel<1||c.QueryParallel>32||c.QueryGpuLayers<0||c.QueryGpuLayers>999)throw new Exception("Query 运行参数越界");
      }
   }
   public static Unit[] Units(HubConfig c){
      var list=new List<Unit>{
        new Unit("Embedding",Resolve(c.EmbeddingPath),c.EmbeddingId,c.EmbeddingPort,c.EmbeddingParallel),
        new Unit("Reranker",Resolve(c.RerankerPath),c.RerankerId,c.RerankerPort,c.RerankerParallel)};
      if(c.QueryEnabled)list.Add(new Unit("Query",Resolve(c.QueryPath),c.QueryId,c.QueryPort,c.QueryParallel));
      return list.ToArray();
   }
 }
 public static class Logger {
   static readonly object gate=new object();
   public static void Write(string line){
     try{
       Directory.CreateDirectory(Paths.LogDir);
       lock(gate){File.AppendAllText(Paths.UiLog,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"  "+line+Environment.NewLine,Encoding.UTF8);}
     }catch{}
   }
   public static string Tail(string name,int maxLines=12){
     try {if(!File.Exists(name))return "暂无日志";return string.Join(Environment.NewLine,File.ReadLines(name,Encoding.UTF8).Reverse().Take(maxLines).Reverse());}catch{return "日志暂不可读";}
   }
 }
 public static class ConfigText {
   public static string Build(HubConfig c){
     return "【Local RAG Hub｜酒馆本地 Agent 配置】\r\n"+
     "用途：本地语义检索服务，不是正文聊天/角色扮演生成模型。请勿填入聊天模型 API。\r\n\r\n"+
     "Embedding 模型："+c.EmbeddingId+"\r\n"+
     "Embedding Base URL：http://127.0.0.1:"+c.EmbeddingPort+"/v1\r\n"+
     "Embedding 完整 Endpoint：http://127.0.0.1:"+c.EmbeddingPort+"/v1/embeddings\r\n"+
     "模型权重："+Paths.Resolve(c.EmbeddingPath)+"\r\n"+
      "注意：向量维度必须通过实际响应核对。Qwen3-Embedding-8B 为 4096 维；其他模型（含不同尺寸/量化/pooling）维度可能不同，不能照搬 4096。\r\n\r\n"+
     "Reranker 模型："+c.RerankerId+"\r\n"+
     "Reranker Base URL：http://127.0.0.1:"+c.RerankerPort+"/v1\r\n"+
     "Reranker 完整 Endpoint：http://127.0.0.1:"+c.RerankerPort+"/v1/rerank\r\n"+
     "模型权重："+Paths.Resolve(c.RerankerPath)+"\r\n\r\n"+
     "程序文件："+Paths.Resolve(c.ServerPath)+"\r\n"+
      "启用的服务仅监听 127.0.0.1，启动器不设 API Key；可使用 local-rag 占位字符串。\r\n"+
      (c.QueryEnabled?"Query Chat 模型："+c.QueryId+"\r\nQuery Endpoint：http://127.0.0.1:"+c.QueryPort+"/v1/chat/completions\r\nQuery 模型列表：http://127.0.0.1:"+c.QueryPort+"/v1/models\r\n用于检索前 Query 重写，stream:false；不会自动修改外部插件。\r\n":"")+
     "运行检查：http://127.0.0.1:"+c.EmbeddingPort+"/health 和 http://127.0.0.1:"+c.RerankerPort+"/health\r\n"+
     "奶龙(龙血玄黄/shujuku)支持完整 Endpoint 格式；狐忆与其他记忆插件须根据其自身接口契约核验。\r\n"+
     "重要：更换 Embedding 模型、量化方式或维度后旧向量索引不能假定兼容！生产数据先备份、隔离验证、按插件流程重建索引；未经用户授权不可自行修改世界书、聊天和正式数据库。\r\n"+
     "请先仅做连接与健康验证，不修改生产环境。";
   }
 }
 internal static class Program {
   [STAThread] static void Main(string[] args){
     if(args.Length>0 && args[0]=="--selftest"){
        RunSelfTest();return;
     }
    bool fresh;using(var mutex=new Mutex(true,@"Local\LocalRagHubSingleInstance_V03",out fresh)){
      if(!fresh){MessageBox.Show("本地检索服务台已经打开。","Local RAG Hub");return;}
       Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
       Application.Run(new MainWindow());
     }
   }
   // Headless self-test: MUST NOT create any window, form, timer or dialog.
   // Verifiable without a GUI by reading selftest-result.txt and the process exit code.
   static void RunSelfTest(){
     int code=0;
     var sb=new StringBuilder();
     sb.AppendLine("LocalRagHub self-test (headless, no GUI)");
     sb.AppendLine("Time: "+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
     sb.AppendLine("ExeDir: "+Paths.AppDir);
     try{
       var c=Paths.Read();
       sb.AppendLine("ConfigFile: "+Paths.ConfigFile+" ("+(File.Exists(Paths.ConfigFile)?"present":"absent, defaults used")+")");
       sb.AppendLine("llama-server: "+Paths.Resolve(c.ServerPath)+" ["+(File.Exists(Paths.Resolve(c.ServerPath))?"OK":"MISSING")+"]");
       sb.AppendLine("Embedding: "+c.EmbeddingId+" port="+c.EmbeddingPort+" pooling="+c.EmbeddingPooling+" ["+(File.Exists(Paths.Resolve(c.EmbeddingPath))?"OK":"MISSING")+"]");
       sb.AppendLine("Reranker: "+c.RerankerId+" port="+c.RerankerPort+" ["+(File.Exists(Paths.Resolve(c.RerankerPath))?"OK":"MISSING")+"]");
        sb.AppendLine("Query: "+(c.QueryEnabled?"ENABLED":"DISABLED")+" model="+c.QueryId+" port="+c.QueryPort+" ctx="+c.QueryContext+" ngl="+c.QueryGpuLayers);
       try{Paths.Validate(c);sb.AppendLine("ConfigValidation: PASS");}
       catch(Exception ve){sb.AppendLine("ConfigValidation: NOT-READY ("+ve.Message+")");}
       var miss=Paths.Missing(c);
       sb.AppendLine("Missing: "+(miss.Count==0?"none":string.Join(", ",miss.ToArray())));
     }catch(Exception ex){sb.AppendLine("ERROR: "+ex.GetBaseException().Message);code=2;}
     sb.AppendLine("Result: SELFTEST_OK_NO_GUI");
     var report=sb.ToString();
     try{File.WriteAllText(Path.Combine(Paths.AppDir,"selftest-result.txt"),report,Encoding.UTF8);}catch{}
     try{Console.Out.Write(report);Console.Out.Flush();}catch{}
     Environment.ExitCode=code;
   }
 }
}

namespace LocalRagHub {
 public static class Backend {
   static string Run(string exe,string args,int ms){
     using(var p=new Process()){
       var sb=new StringBuilder();
       p.StartInfo.FileName=exe;p.StartInfo.Arguments=args;p.StartInfo.UseShellExecute=false;
       p.StartInfo.CreateNoWindow=true;p.StartInfo.RedirectStandardOutput=true;
       p.OutputDataReceived+=(s,e)=>{if(e.Data!=null)lock(sb){sb.AppendLine(e.Data);}};
       p.Start();p.BeginOutputReadLine();
       if(!p.WaitForExit(ms)){try{p.Kill();}catch{}throw new TimeoutException("探测命令超时："+exe);}
       p.WaitForExit();lock(sb){return sb.ToString();}
     }
   }
   static int Listener(int port){
     try{
       string output=Run(Path.Combine(Environment.SystemDirectory,"netstat.exe"),"-ano -p tcp",4000);
       foreach(string raw in output.Split('\n')){
         var cells=Regex.Split(raw.Trim(),@"\s+");
         if(cells.Length<5 || cells[0].ToUpperInvariant()!="TCP"||cells[3].ToUpperInvariant()!="LISTENING")continue;
         string remote=cells[1];
         if(remote=="127.0.0.1:"+port || remote=="[::1]:"+port || remote=="0.0.0.0:"+port || remote=="[::]:"+port){
           int pid;if(int.TryParse(cells[4],out pid))return pid;
         }
       }
     }catch(Exception ex){Logger.Write("[monitor] netstat "+ex.Message);}
     return 0;
   }
   static bool Owned(int pid,Unit unit,HubConfig c){
     if(pid<=0)return false;
     try {
       using(var search=new ManagementObjectSearcher("SELECT ExecutablePath,CommandLine FROM Win32_Process WHERE ProcessId="+pid)){
         foreach(ManagementObject o in search.Get()){
           string actual=Convert.ToString(o["ExecutablePath"]),cmd=Convert.ToString(o["CommandLine"]);
           return string.Equals(actual,Paths.Resolve(c.ServerPath),StringComparison.OrdinalIgnoreCase)
                && cmd.IndexOf(unit.File,StringComparison.OrdinalIgnoreCase)>=0
                && Regex.IsMatch(cmd,@"(?:--port|-p)\s+"+unit.Port+@"(?:\s|$)",RegexOptions.IgnoreCase)
                && cmd.IndexOf("--host 127.0.0.1",StringComparison.OrdinalIgnoreCase)>=0;
         }
       }
     }catch{}
     return false;
   }
   static int OurPid(Unit unit,HubConfig c){
     try {
       foreach(Process p in Process.GetProcessesByName("llama-server")){
         if(Owned(p.Id,unit,c))return p.Id;
       }
     }catch{}
     return 0;
   }
   static bool IsHealthy(int port){
     try{
       var r=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:"+port+"/health");
       r.Timeout=1200;r.ReadWriteTimeout=1200;r.Proxy=null;
       using(var resp=(HttpWebResponse)r.GetResponse())
       using(var sr=new StreamReader(resp.GetResponseStream())){
         return (int)resp.StatusCode==200 && sr.ReadToEnd().IndexOf("\"ok\"",StringComparison.OrdinalIgnoreCase)>=0;
       }
     }catch{return false;}
   }
   static bool HasChatModel(int port,string alias){
      try{
        var req=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:"+port+"/v1/models");
        req.Timeout=1200;req.ReadWriteTimeout=1200;req.Proxy=null;
        using(var resp=(HttpWebResponse)req.GetResponse())
        using(var sr=new StreamReader(resp.GetResponseStream())){
          if((int)resp.StatusCode!=200)return false;
          var json=new JavaScriptSerializer().DeserializeObject(sr.ReadToEnd()) as Dictionary<string,object>;
          if(json==null||!json.ContainsKey("data"))return false;
          var entries=json["data"] as object[];
          if(entries==null)return false;
          foreach(var item in entries){
            var row=item as Dictionary<string,object>;
            if(row!=null&&row.ContainsKey("id")&&String.Equals(Convert.ToString(row["id"]),alias,StringComparison.Ordinal))return true;
          }
          return false;
        }
      }catch{return false;}
   }
   public static UnitStatus Check(Unit unit,HubConfig c){
     var u=new UnitStatus();int pid=Listener(unit.Port);
     if(pid>0){
       u.Pid=pid;
       if(!Owned(pid,unit,c)){u.State="FOREIGN";u.Detail="端口被其他程序/配置占用，禁止停止";return u;}
       u.State=IsHealthy(unit.Port)&&(unit.Role!="Query"||HasChatModel(unit.Port,unit.Alias))?"READY":"LOADING";
       if(unit.Role=="Query"&&u.State=="LOADING")u.Detail="等待 /health 与 /v1/models";
     }else{
       u.Pid=OurPid(unit,c);
       u.State=u.Pid>0?"LOADING":"STOPPED";
     }
     if(u.Pid!=0){try{u.Since=Process.GetProcessById(u.Pid).StartTime;}catch{}}
     return u;
   }
   static string Quote(string v){return "\""+v.Replace("\"","\\\"")+"\"";}
   public static string Start(Unit unit,HubConfig c){
     UnitStatus start=Check(unit,c);
     if(start.State=="READY")return unit.Role+" 已在运行 (PID "+start.Pid+")";
     if(start.State=="FOREIGN")throw new Exception(unit.Role+" 的端口 "+unit.Port+" 已被其他进程/不同配置使用");
     if(!File.Exists(Paths.Resolve(c.ServerPath)))throw new Exception("找不到 llama.cpp 程序");
     if(!File.Exists(unit.File))throw new Exception(unit.Role+" 模型文件不存在："+unit.File);
     if(start.State=="STOPPED"){
       Directory.CreateDirectory(Paths.LogDir);
       string modelLog=Path.Combine(Paths.LogDir,unit.Role.ToLowerInvariant()+"-server.log");
       var args=new StringBuilder();
       args.Append("-m ").Append(Quote(unit.File)).Append(" ");
       if(unit.Role=="Embedding")args.Append("--embedding --pooling ").Append(c.EmbeddingPooling).Append(" ");
       else if(unit.Role=="Reranker")args.Append("--embedding --reranking --pooling rank ");
       else {
          args.Append("--jinja ");
          if(c.QueryDisableThinking)args.Append("--reasoning off --chat-template-kwargs ").Append(Quote("{\"enable_thinking\":false}")).Append(" ");
        }
       args.Append("--host 127.0.0.1 --port ").Append(unit.Port);
       args.Append(" -c ").Append(unit.Role=="Query"?c.QueryContext:c.Context).Append(" -np ").Append(unit.Parallel);
       args.Append(" -ngl ").Append(unit.Role=="Query"?c.QueryGpuLayers:c.GpuLayers).Append(" -kvu --no-webui ");
       args.Append("--alias ").Append(Quote(unit.Alias));
       args.Append(" --log-file ").Append(Quote(modelLog));
       args.Append(" --log-verbosity 3");
       var psi=new ProcessStartInfo(Paths.Resolve(c.ServerPath),args.ToString()){
         WorkingDirectory=Path.GetDirectoryName(Paths.Resolve(c.ServerPath)),UseShellExecute=false,CreateNoWindow=true
       };
       var proc=Process.Start(psi);
       Logger.Write("["+unit.Role+"] 启动请求 PID "+proc.Id+" 端口 "+unit.Port);
     }
     for(int n=0;n<180;n++){
       Thread.Sleep(500);
       UnitStatus s=Check(unit,c);
       if(s.State=="READY")return unit.Role+" READY  PID "+s.Pid;
       if(s.State=="FOREIGN")throw new Exception(unit.Role+" 端口归属异常："+s.Detail);
       if(s.State=="STOPPED" && n>6)throw new Exception(unit.Role+" 进程提前退出，请查看服务日志");
     }
     throw new TimeoutException(unit.Role+" 加载超过90秒；面板仍可使用，请核查模型服务日志");
   }
   public static string Stop(Unit unit,HubConfig c){
     UnitStatus state=Check(unit,c);
     if(state.State=="FOREIGN")return unit.Role+"：非本应用进程，未触碰";
     if(state.Pid<=0)return unit.Role+" 已停止";
     if(!Owned(state.Pid,unit,c))throw new Exception(unit.Role+" 进程归属校验失败，不允许结束");
     var proc=Process.GetProcessById(state.Pid);proc.Kill();proc.WaitForExit(5000);
     Logger.Write("["+unit.Role+"] 关闭 PID "+state.Pid);
     return unit.Role+" 已停止 PID "+state.Pid;
   }
   public static string Gpu(){
     try{
       string[] x=Run(Path.Combine(Environment.SystemDirectory,"nvidia-smi.exe"),
        "--query-gpu=name,memory.used,memory.total,memory.free,utilization.gpu,temperature.gpu,power.draw --format=csv,noheader,nounits",4000).Split(',');
       if(x.Length<7)return "暂无 GPU 数据";
       return string.Join("|",x.Select(t=>t.Trim()).ToArray());
     }catch{return "";}
   }
 }
}

namespace LocalRagHub {
 public static class Theme {
   public static readonly Color Bg=Color.FromArgb(10,22,32), Card=Color.FromArgb(17,36,50), Edge=Color.FromArgb(43,77,97), Muted=Color.FromArgb(178,195,205);
   public static readonly Color Green=Color.FromArgb(38,234,160), Cyan=Color.FromArgb(98,210,226), Yellow=Color.FromArgb(255,197,84), Red=Color.FromArgb(252,106,107), Button=Color.FromArgb(34,59,76);
   public static Font Font(int size,bool bold=false){return new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular);}
   public static Label Label(Control c,string txt,int x,int y,int w,int h,int size,Color color,bool bold=false){
     var l=new Label{Text=txt,Location=new Point(x,y),Size=new Size(w,h),ForeColor=color,BackColor=Color.Transparent,Font=Font(size,bold),AutoEllipsis=true};
     c.Controls.Add(l);return l;
   }
   public static GraphicsPath Shape(Rectangle r,int rad){
     var p=new GraphicsPath();int d=rad*2;
     p.AddArc(r.Left,r.Top,d,d,180,90);
     p.AddArc(r.Right-d,r.Top,d,d,270,90);
     p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);
     p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
   }
 }
 public class Card:Panel {
   public bool Glow=false; public Color Line=Theme.Edge;public int Corner=14;
   public Card(int x,int y,int w,int h) {
     Location=new Point(x,y);Size=new Size(w,h);BackColor=Theme.Card;
     DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);
   }
   protected override void OnPaintBackground(PaintEventArgs e){
     e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
     using(var path=Theme.Shape(new Rectangle(1,1,Width-3,Height-3),Corner))
     using(var b=new SolidBrush(BackColor))e.Graphics.FillPath(b,path);
   }
   protected override void OnPaint(PaintEventArgs e){
     base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
     using(var path=Theme.Shape(new Rectangle(1,1,Width-3,Height-3),Corner))
     using(var pen=new Pen(Glow?Theme.Green:Line,Glow?2:1)){e.Graphics.DrawPath(pen,path);}
   }
 }
 public class ActionButton:Button {
   public bool Primary=false;public bool Destructive=false; bool hovering=false;
   public ActionButton(string text,int x,int y,int w,int h,bool primary=false){
      Text=text;Location=new Point(x,y);Size=new Size(w,h);
      Primary=primary;BackColor=Theme.Button;ForeColor=Color.White;Font=Theme.Font(10,true);
      FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;TabStop=true;
      SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
   }
   protected override void OnMouseEnter(EventArgs e){hovering=true;Invalidate();base.OnMouseEnter(e);}
   protected override void OnMouseLeave(EventArgs e){hovering=false;Invalidate();base.OnMouseLeave(e);}
   protected override void OnPaint(PaintEventArgs e){
     e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
     Color basecolor=Primary?Color.FromArgb(10,139,101):(Destructive?Color.FromArgb(73,47,58):Theme.Button);
     if(hovering&&Enabled)basecolor=ControlPaint.Light(basecolor,.18f);
     if(!Enabled)basecolor=Color.FromArgb(39,52,61);
     using(var path=Theme.Shape(new Rectangle(1,1,Width-3,Height-3),10)){
       using(var br=new LinearGradientBrush(ClientRectangle,basecolor,ControlPaint.Dark(basecolor,.22f),LinearGradientMode.Vertical))e.Graphics.FillPath(br,path);
       using(var pen=new Pen(Primary?Theme.Green:Theme.Edge,1)){e.Graphics.DrawPath(pen,path);}
     }
     TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?Color.White:Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
   }
 }
 public class Bar:Control {
   public float Portion=0.0f; public Bar(int x,int y,int w,int h){Location=new Point(x,y);Size=new Size(w,h);BackColor=Theme.Card;DoubleBuffered=true;}
   protected override void OnPaint(PaintEventArgs e){
     e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
     Rectangle whole=new Rectangle(0,1,Width-1,Height-3);
     using(var path=Theme.Shape(whole,Math.Max(3,Height/3)))
     using(var br=new SolidBrush(Color.FromArgb(45,73,91)))e.Graphics.FillPath(br,path);
     float x=Math.Max(0,Math.Min(1,Portion));
     if(x>0){var fill=new Rectangle(0,1,Math.Max(7,(int)(Width*x)),Height-3);using(var p=Theme.Shape(fill,Math.Max(3,Height/3)))using(var b=new SolidBrush(Theme.Green))e.Graphics.FillPath(b,p);}
   }
 }
  public class SettingsDialog:Form {
    public HubConfig Value;
    TextBox server,embfile,rerfile,embid,rerid;ComboBox pooling;
    NumericUpDown embport,rerport,context,embparallel,rerparallel,gpu;
    Panel main;Label hint;
    public SettingsDialog(HubConfig conf){
      Value=Paths.Clone(conf);
      Text="Local RAG Hub · 设置";ClientSize=new Size(920,700);
      BackColor=Theme.Bg;ForeColor=Color.White;Font=Theme.Font(10);
      AutoScaleMode=AutoScaleMode.None;
      StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
      Theme.Label(this,"⚙  模型与服务配置",25,15,860,40,19,Color.White,true);
      Theme.Label(this,"修改后点「保存配置」。正在运行的模型需先在主窗口停止，再切换模型或端口。",28,57,860,26,10,Theme.Muted);
      var back=new Card(22,92,876,456);Controls.Add(back);main=back;
      int y=16;
      server=LineField("① llama-server.exe 路径",Value.ServerPath,ref y,true,"Executable (*.exe)|*.exe");
      embfile=LineField("② Embedding GGUF 路径",Value.EmbeddingPath,ref y,true,"GGUF 模型 (*.gguf)|*.gguf");
      rerfile=LineField("③ Reranker GGUF 路径",Value.RerankerPath,ref y,true,"GGUF 模型 (*.gguf)|*.gguf");
      embid=LineField("④ Embedding 模型 ID (可自定义)",Value.EmbeddingId,ref y,false,"");
      rerid=LineField("⑤ Reranker 模型 ID (可自定义)",Value.RerankerId,ref y,false,"");
      embport=Number("Embedding 端口",Value.EmbeddingPort,18,316,1024,65535);
      rerport=Number("Reranker 端口",Value.RerankerPort,300,316,1024,65535);
      context=Number("Context Tokens",Value.Context,582,316,512,131072);
      embparallel=Number("Embedding 并行槽 (-np)",Value.EmbeddingParallel,18,378,1,32);
      rerparallel=Number("Reranker 并行槽 (-np)",Value.RerankerParallel,300,378,1,32);
      gpu=Number("GPU Layers (-ngl)",Value.GpuLayers,582,378,0,999);
      Theme.Label(this,"⑥ Embedding 池化策略 (Qwen3-Embedding=last)：",28,560,420,26,10,Theme.Muted);
      pooling=new ComboBox{Location=new Point(452,555),Size=new Size(180,32),DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Color.FromArgb(13,29,39),ForeColor=Color.White,Font=Theme.Font(10)};
      pooling.Items.AddRange(new object[]{"last","mean","cls","none"});pooling.SelectedItem=Value.EmbeddingPooling;Controls.Add(pooling);
      hint=Theme.Label(this,"",28,596,860,30,9,Theme.Muted);
      Theme.Label(this,"配置保存到程序目录 config.json，不修改 TT 酒馆、奶龙或狐忆。",28,624,560,24,9,Theme.Muted);
      var save=new ActionButton("保存配置",662,644,236,36,true);
      var cancel=new ActionButton("取消",502,644,140,36);
       var queryButton=new ActionButton("Query 重写设置（可选）",28,644,245,36);
      Controls.Add(save);Controls.Add(cancel);
       Controls.Add(queryButton);
       queryButton.Click+=(s,e)=>{using(var d=new QuerySettingsDialog(Value)){if(d.ShowDialog(this)==DialogResult.OK)Value=Paths.Clone(d.Value);}};
      cancel.Click+=(s,e)=>{DialogResult=DialogResult.Cancel;Close();};
      save.Click+=(s,e)=>{
        try{
          Value.ServerPath=server.Text.Trim();Value.EmbeddingPath=embfile.Text.Trim();Value.RerankerPath=rerfile.Text.Trim();
          Value.EmbeddingId=embid.Text.Trim();Value.RerankerId=rerid.Text.Trim();
          Value.EmbeddingPort=(int)embport.Value;Value.RerankerPort=(int)rerport.Value;
          Value.Context=(int)context.Value;Value.EmbeddingParallel=(int)embparallel.Value;Value.RerankerParallel=(int)rerparallel.Value;Value.GpuLayers=(int)gpu.Value;Value.EmbeddingPooling=Convert.ToString(pooling.SelectedItem);
          Paths.Validate(Value);Paths.Save(Value);DialogResult=DialogResult.OK;Close();
        }catch(Exception ex){UpdateHint();MessageBox.Show(this,ex.Message,"配置未保存",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
      };
      server.TextChanged+=(s,e)=>UpdateHint();embfile.TextChanged+=(s,e)=>UpdateHint();rerfile.TextChanged+=(s,e)=>UpdateHint();
      UpdateHint();
    }
    void UpdateHint(){
      try{
        bool s=File.Exists(Paths.Resolve(server.Text.Trim()));
        bool e=File.Exists(Paths.Resolve(embfile.Text.Trim()));
        bool r=File.Exists(Paths.Resolve(rerfile.Text.Trim()));
        bool ok=s&&e&&r;
        hint.Text="文件检测：  llama-server "+(s?"✔":"✘")+"    Embedding 权重 "+(e?"✔":"✘")+"    Reranker 权重 "+(r?"✔":"✘")+(ok?"     ——  三项齐全，可以保存并启动":"     ——  请补全缺失项（✘）后再启动");
        hint.ForeColor=ok?Theme.Green:Theme.Yellow;
      }catch{hint.Text="文件检测：路径无效";hint.ForeColor=Theme.Yellow;}
    }
    TextBox LineField(string caption,string value,ref int y,bool browse,string filter){
       Theme.Label(main,caption,18,y+3,214,26,10,Theme.Muted);
       var tb=new TextBox{Text=value,Location=new Point(238,y),Size=new Size(browse?556:604,30),BackColor=Color.FromArgb(13,29,39),ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle,Font=Theme.Font(10)};
       main.Controls.Add(tb);
       if(browse){
         var btn=new ActionButton("…",802,y-1,56,32); main.Controls.Add(btn);
         btn.Click+=(s,e)=>{using(var dlg=new OpenFileDialog{Filter=filter,CheckFileExists=true}){if(dlg.ShowDialog(this)==DialogResult.OK)tb.Text=dlg.FileName;}};
       }
       y+=60;return tb;
    }
    NumericUpDown Number(string caption,int value,int x,int y,int min,int max){
      Theme.Label(main,caption,x,y,240,22,9,Theme.Muted);
      var n=new NumericUpDown{Minimum=min,Maximum=max,Value=Math.Max(min,Math.Min(max,value)),Location=new Point(x,y+22),Size=new Size(230,29),BackColor=Color.FromArgb(13,29,39),ForeColor=Color.White,Font=Theme.Font(10)};
      main.Controls.Add(n);return n;
    }
  }
}

namespace LocalRagHub {
 public class QuerySettingsDialog:Form {
   public HubConfig Value;
   CheckBox enabled,disableThink; TextBox path,alias;
   NumericUpDown port,ctx,parallel,gpu;
   public QuerySettingsDialog(HubConfig c){
     Value=Paths.Clone(c);
     Text="Local RAG Hub · Query 重写";ClientSize=new Size(700,510);
     BackColor=Theme.Bg;ForeColor=Color.White;Font=Theme.Font(10);
     StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
     Theme.Label(this,"Query Chat 模型（可选第三服务）",24,12,650,38,18,Color.White,true);
     Theme.Label(this,"默认关闭；不下载 GGUF，不修改前两个服务或向量索引。",24,54,650,30,10,Theme.Muted);
     enabled=new CheckBox{Text="启用 Query 重写服务",Checked=Value.QueryEnabled,Location=new Point(26,97),Size=new Size(420,28),ForeColor=Theme.Green,BackColor=Theme.Bg};Controls.Add(enabled);
     Theme.Label(this,"Chat GGUF 文件",26,141,210,24,10,Theme.Muted);
     path=new TextBox{Text=Value.QueryPath,Location=new Point(26,168),Size=new Size(584,29),BackColor=Theme.Card,ForeColor=Color.White};Controls.Add(path);
     var browse=new ActionButton("…",615,167,58,32);Controls.Add(browse);
     browse.Click+=(s,e)=>{using(var d=new OpenFileDialog{Filter="GGUF 模型 (*.gguf)|*.gguf",CheckFileExists=true}){if(d.ShowDialog(this)==DialogResult.OK)path.Text=d.FileName;}};
     Theme.Label(this,"模型 ID（/v1/models 和 Chat Completions）",26,214,632,24,10,Theme.Muted);
     alias=new TextBox{Text=Value.QueryId,Location=new Point(26,243),Size=new Size(647,30),BackColor=Theme.Card,ForeColor=Color.White};Controls.Add(alias);
     port=Num("端口",Value.QueryPort,26,291,1024,65535);
     ctx=Num("上下文",Value.QueryContext,194,291,512,131072);
     parallel=Num("并行槽",Value.QueryParallel,362,291,1,32);
     gpu=Num("GPU Layers",Value.QueryGpuLayers,530,291,0,999);
     disableThink=new CheckBox{Text="禁用模型思考（支持的模型模板）",Checked=Value.QueryDisableThinking,Location=new Point(26,370),Size=new Size(620,29),ForeColor=Theme.Cyan,BackColor=Theme.Bg};Controls.Add(disableThink);
     Theme.Label(this,"GPU Layers=0 使用 CPU；速度与显存需实际测试。",26,404,646,23,9,Theme.Muted);
     var cancel=new ActionButton("取消",358,442,147,42);var ok=new ActionButton("确定",516,442,158,42,true);
     Controls.Add(cancel);Controls.Add(ok);
     cancel.Click+=(s,e)=>{DialogResult=DialogResult.Cancel;Close();};
     ok.Click+=(s,e)=>{
       Value.QueryEnabled=enabled.Checked;Value.QueryDisableThinking=disableThink.Checked;Value.QueryPath=path.Text.Trim();Value.QueryId=alias.Text.Trim();
       Value.QueryPort=(int)port.Value;Value.QueryContext=(int)ctx.Value;
       Value.QueryParallel=(int)parallel.Value;Value.QueryGpuLayers=(int)gpu.Value;
       DialogResult=DialogResult.OK;Close();
     };
   }
   NumericUpDown Num(string name,int value,int x,int y,int min,int max){
     Theme.Label(this,name,x,y,160,22,9,Theme.Muted);
     var n=new NumericUpDown{Minimum=min,Maximum=max,Value=Math.Max(min,Math.Min(max,value)),Location=new Point(x,y+26),Size=new Size(148,28),BackColor=Theme.Card,ForeColor=Color.White};
     Controls.Add(n);return n;
   }
 }
}

namespace LocalRagHub {
 public class MainWindow:Form {
    HubConfig cfg;UnitStatus[] current={new UnitStatus(),new UnitStatus(),new UnitStatus{State="DISABLED"}};
    bool configured=true;
   readonly Color W=Color.White;
    Label allstatus,embstate,rerstate,querystate,embpid,rerpid,querypid,embtime,rertime,querytime,embport,rerport,queryport,gpuname,vramnumbers,freemem,gpuusage,temperature,power,refresh;
   Bar memBar,utilBar;RichTextBox logView;ActionButton go,stop,restart,copy,settings,querySettings,viewlogs,models;
    ActionButton embStart,embStop,rerStart,rerStop,queryStart,queryStop;
   System.Windows.Forms.Timer clock;int poll=0,working=0;
   public MainWindow(){
      cfg=Paths.Read();
      Text="Local RAG Hub · 本地检索服务台";
      ClientSize=new Size(1290,919);StartPosition=FormStartPosition.CenterScreen;
      BackColor=Theme.Bg; ForeColor=Color.White;Font=Theme.Font(10);
      AutoScaleMode=AutoScaleMode.None;
      FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;DoubleBuffered=true;
      try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
      DrawUI();
      Shown+=(s,e)=>{
        bool freshRun=!File.Exists(Paths.ConfigFile);
        var miss=Paths.Missing(cfg);configured=miss.Count==0;
        LoadHistory();
        Log("启动器已就绪。当前配置："+cfg.EmbeddingId+" / "+cfg.RerankerId+(cfg.QueryEnabled?" / "+cfg.QueryId:"（Query 已关闭）"));
        if(!configured){
          Log("尚未完成配置，缺少："+string.Join("、",miss.ToArray()));
          allstatus.Text="⚠ 尚未完成配置：请点击右上角「设置」选择 llama-server 与两个 GGUF";
          allstatus.ForeColor=Theme.Yellow;
        }
        clock.Start();RefreshStatus();
        if(freshRun&&!configured){try{BeginInvoke((Action)(()=>OpenSettings()));}catch{}}
      };
      FormClosing+=(s,e)=>{
        if(Interlocked.CompareExchange(ref working,0,0)>0){
          MessageBox.Show(this,"模型操作仍在执行中，操作完成后再关闭启动器。","请稍候");
          e.Cancel=true;
        }else{clock.Stop();}
      };
   }
   Card CardAt(int x,int y,int w,int h){var card=new Card(x,y,w,h);Controls.Add(card);return card;}
   ActionButton ButtonOn(Control par,string text,int x,int y,int w,int h,bool primary=false){
     var b=new ActionButton(text,x,y,w,h,primary);par.Controls.Add(b);return b;
   }
   void DrawUI(){
      var top=CardAt(23,15,1243,173);
      Theme.Label(top,"⬡",23,27,75,80,36,Theme.Cyan,true);
      Theme.Label(top,"Local RAG Hub",94,19,545,47,25,W,true);
      Theme.Label(top,"本地检索服务台 · 一键启动 · 实时监控",98,71,535,37,13,Theme.Muted);
      Theme.Label(top,"Embedding / Reranker / Query Chat · 独立服务控制",98,115,660,28,10,Theme.Muted);
      settings=ButtonOn(top,"⚙  设置",1098,18,120,38,false);
       querySettings=ButtonOn(top,"◇ Query设置",947,18,140,38,false);
      var chip=new Card(682,67,535,85){Line=Theme.Green};top.Controls.Add(chip);
      Theme.Label(chip,"●  服务总状态",22,12,238,25,11,Theme.Muted);
      allstatus=Theme.Label(chip,"正在检查...",22,37,488,35,19,Theme.Green,true);

       var em=CardAt(23,204,400,185);em.Line=Theme.Cyan;
       var rr=CardAt(437,204,400,185);rr.Line=Theme.Cyan;
       var qr=CardAt(851,204,415,185);qr.Line=Theme.Cyan;
       Theme.Label(em,"◈  Embedding",21,18,255,36,15,W,true);
       Theme.Label(rr,"◉  Reranker",21,18,255,36,15,W,true);
       Theme.Label(qr,"◇  Query Chat",21,18,255,36,15,W,true);
       embstate=Theme.Label(em,"检查中...",279,19,110,33,12,Theme.Yellow,true);
       rerstate=Theme.Label(rr,"检查中...",279,19,110,33,12,Theme.Yellow,true);
       querystate=Theme.Label(qr,"DISABLED",291,19,110,33,12,Theme.Muted,true);
       foreach(var card in new Control[]{em,rr,qr}){
         for(int i=0;i<3;i++)Theme.Label(card,i==0?"PID":i==1?"监听端口":"运行时长",20+i*128,85,123,24,10,Theme.Muted);
       }
       embpid=Theme.Label(em,"--",20,113,123,45,13,W,true);embport=Theme.Label(em,"--",148,113,123,45,13,W,true);embtime=Theme.Label(em,"--",276,113,123,45,13,W,true);
       rerpid=Theme.Label(rr,"--",20,113,123,45,13,W,true);rerport=Theme.Label(rr,"--",148,113,123,45,13,W,true);rertime=Theme.Label(rr,"--",276,113,123,45,13,W,true);
       querypid=Theme.Label(qr,"--",20,113,123,45,13,W,true);queryport=Theme.Label(qr,"--",148,113,123,45,13,W,true);querytime=Theme.Label(qr,"--",276,113,123,45,13,W,true);
       embStart=ButtonOn(em,"▶ 启动",20,153,170,27,true);embStop=ButtonOn(em,"■ 停止",209,153,170,27);
       rerStart=ButtonOn(rr,"▶ 启动",20,153,170,27,true);rerStop=ButtonOn(rr,"■ 停止",209,153,170,27);
       queryStart=ButtonOn(qr,"▶ 启动",20,153,170,27,true);queryStop=ButtonOn(qr,"■ 停止",209,153,170,27);

       var gpu=CardAt(23,407,1243,178);
      Theme.Label(gpu,"▥  GPU 资源监控",22,16,410,37,16,W,true);
      gpuname=Theme.Label(gpu,"NVIDIA GPU",860,18,360,31,12,Theme.Cyan,true);
      Theme.Label(gpu,"显存使用",27,68,165,24,11,Theme.Muted);
      memBar=new Bar(27,97,430,19);gpu.Controls.Add(memBar);
      vramnumbers=Theme.Label(gpu,"—",27,123,495,32,11,W);
      Theme.Label(gpu,"可用显存",501,68,190,24,11,Theme.Muted);
      freemem=Theme.Label(gpu,"—",501,100,215,48,16,W,true);
      Theme.Label(gpu,"GPU 负载",751,68,170,24,11,Theme.Muted);
      gpuusage=Theme.Label(gpu,"—",751,99,146,40,17,W,true);
      utilBar=new Bar(750,140,137,9);gpu.Controls.Add(utilBar);
      Theme.Label(gpu,"温度",919,68,130,24,11,Theme.Muted);
      temperature=Theme.Label(gpu,"—",919,100,134,40,16,W,true);
      Theme.Label(gpu,"功率",1083,68,130,24,11,Theme.Muted);
      power=Theme.Label(gpu,"—",1083,100,135,40,16,W,true);

      var logcard=CardAt(23,603,749,251);
      Theme.Label(logcard,"▤  运行日志",19,12,412,36,16,W,true);
      var area=new Card(14,51,721,184){Line=Theme.Edge,BackColor=Color.FromArgb(9,23,33)};
      logcard.Controls.Add(area);
      logView=new RichTextBox{Location=new Point(11,9),Size=new Size(699,165),BackColor=Color.FromArgb(9,23,33),
        ForeColor=Theme.Muted,Font=new Font("Consolas",10),BorderStyle=BorderStyle.None,ReadOnly=true,WordWrap=false,DetectUrls=false};
      area.Controls.Add(logView);
      var action=CardAt(791,603,475,251);
      Theme.Label(action,"▷  服务控制",20,12,429,36,16,W,true);
      go=ButtonOn(action,"▶   启动 / 恢复服务",17,51,438,56,true);
      stop=ButtonOn(action,"■   停止服务",17,118,213,45,false);stop.Destructive=true;
      restart=ButtonOn(action,"↻   重新启动",240,118,215,45,false);
      copy=ButtonOn(action,"▣   一键复制酒馆 Agent 配置",17,177,438,52,true);

      viewlogs=ButtonOn(this,"查看日志",24,868,154,34);
      models=ButtonOn(this,"打开模型目录",190,868,181,34);
      Theme.Label(this,"◈  仅本机 127.0.0.1    ·    按需加载    ·    不会开机自启",398,872,630,26,9,Theme.Muted);
      refresh=Theme.Label(this,"正在连接...",1054,872,216,28,9,Theme.Cyan);

      settings.Click+=(s,e)=>OpenSettings();
       querySettings.Click+=(s,e)=>OpenQuerySettings();
       embStart.Click+=(s,e)=>DoAction("启动","Embedding");
       embStop.Click+=(s,e)=>DoAction("停止","Embedding");
       rerStart.Click+=(s,e)=>DoAction("启动","Reranker");
       rerStop.Click+=(s,e)=>DoAction("停止","Reranker");
       queryStart.Click+=(s,e)=>DoAction("启动","Query");
       queryStop.Click+=(s,e)=>DoAction("停止","Query");
      go.Click+=(s,e)=>DoAction("启动");
      stop.Click+=(s,e)=>DoAction("停止");
      restart.Click+=(s,e)=>DoAction("重启");
      copy.Click+=(s,e)=>CopyAgent();
      viewlogs.Click+=(s,e)=>OpenFolder(Paths.LogDir);
      models.Click+=(s,e)=>OpenFolder(Path.GetDirectoryName(Paths.Resolve(cfg.EmbeddingPath)));
      clock=new System.Windows.Forms.Timer{Interval=5000};clock.Tick+=(s,e)=>RefreshStatus();
       queryStart.Enabled=queryStop.Enabled=cfg.QueryEnabled;
   }
   void LoadHistory(){
     try {
       if(File.Exists(Paths.UiLog)){
          var lines=File.ReadLines(Paths.UiLog,Encoding.UTF8).Reverse().Take(25).Reverse();
          foreach(string row in lines)AppendLog(row);
       }
     }catch{}
   }
   void AppendLog(string line){
     if(IsDisposed)return;
     logView.AppendText(line+Environment.NewLine);
     if(logView.TextLength>25000)logView.Text=logView.Text.Substring(logView.TextLength-18000);
     logView.SelectionStart=logView.TextLength;logView.ScrollToCaret();
   }
   void Log(string message){
     Logger.Write(message);
     var msg="["+DateTime.Now.ToString("HH:mm:ss")+"] "+message;
     if(IsDisposed)return;
     if(InvokeRequired){try{BeginInvoke((Action)(()=>AppendLog(msg)));}catch{}}
     else AppendLog(msg);
   }
   void OpenFolder(string folder){
     try{if(!Directory.Exists(folder))throw new DirectoryNotFoundException(folder);Process.Start("explorer.exe","\""+folder+"\"");}
     catch(Exception ex){MessageBox.Show(this,ex.Message,"无法打开目录");}
   }
   void OpenSettings(){
     if(Interlocked.CompareExchange(ref working,0,0)>0){MessageBox.Show(this,"先完成当前操作再编辑设置。");return;}
     if(current.Any(x=>x.State=="READY"||x.State=="LOADING")){
       MessageBox.Show(this,"当前仍有模型在运行。为避免新旧模型路径/端口混淆，请先点击「停止服务」，再保存新配置。","先停止模型",MessageBoxButtons.OK,MessageBoxIcon.Information);
       return;
     }
      using(var dlg=new SettingsDialog(cfg)){
        if(dlg.ShowDialog(this)==DialogResult.OK){
          cfg=Paths.Clone(dlg.Value);Log("配置已更新，并保存到 config.json");
          configured=Paths.Missing(cfg).Count==0;
          RefreshStatus();
        }
      }
   }
   void OpenQuerySettings(){
      if(Interlocked.CompareExchange(ref working,0,0)>0){MessageBox.Show(this,"先完成当前服务操作。");return;}
      if(current.Length>2 && (current[2].State=="READY"||current[2].State=="LOADING")){
        MessageBox.Show(this,"先停止 Query 服务即可更改它的模型；Embedding/Reranker 无需停止。","仅需停止 Query");return;
      }
      using(var d=new QuerySettingsDialog(cfg)){
        if(d.ShowDialog(this)!=DialogResult.OK)return;
        try{
          Paths.Validate(d.Value);Paths.Save(d.Value);
          cfg=Paths.Clone(d.Value);configured=Paths.Missing(cfg).Count==0;
          Log("Query 配置已保存；QueryEnabled="+cfg.QueryEnabled+"；不会自动启动模型。");RefreshStatus();
          ToggleActions(false);
        }catch(Exception ex){MessageBox.Show(this,ex.Message,"Query 配置尚未保存",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
      }
   }
   void CopyAgent(){
     try {Clipboard.SetText(ConfigText.Build(cfg));Log("已复制酒馆本地 Agent 配置（使用当前设置）");refresh.Text="✔ 已复制配置";}
     catch(Exception ex){MessageBox.Show(this,"无法写入剪贴板："+ex.Message);}
   }
   void ToggleActions(bool busy) {
     go.Enabled=stop.Enabled=restart.Enabled=settings.Enabled=querySettings.Enabled=embStart.Enabled=embStop.Enabled=rerStart.Enabled=rerStop.Enabled=queryStart.Enabled=queryStop.Enabled=!busy;
      queryStart.Enabled=queryStop.Enabled=(!busy&&cfg.QueryEnabled);
     go.Text=busy?"⌛  后台操作中，窗口仍可监控":"▶   启动 / 恢复服务";
   }
   void DoAction(string kind,string role=null){
     if(Interlocked.CompareExchange(ref working,1,0)!=0)return;
     ToggleActions(true);Log("开始："+kind+(role==null?"全部服务":role));
     var config=Paths.Clone(cfg);var units=Paths.Units(config).Where(u=>role==null||u.Role==role).ToArray();
      if(units.Length==0){Log("["+role+"] 未启用，先在 Query 设置中启用。");Interlocked.Exchange(ref working,0);ToggleActions(false);return;}
     Task.Run(()=>{
       foreach(var u in units){
         try {
           if(kind=="停止")Log(Backend.Stop(u,config));
           else if(kind=="重启"){Log(Backend.Stop(u,config));Log(Backend.Start(u,config));}
           else Log(Backend.Start(u,config));
         }catch(Exception ex){Log("["+u.Role+"] ERROR "+ex.Message);}
         RefreshStatus();
       }
     }).ContinueWith(t=>{
       Interlocked.Exchange(ref working,0);
       if(t.Exception!=null)Log("任务发生异常："+t.Exception.GetBaseException().Message);
       try{if(!IsDisposed)BeginInvoke((Action)(()=>{ToggleActions(false);RefreshStatus();}));}catch{}
     });
   }
   void RefreshStatus(){
     if(Interlocked.CompareExchange(ref poll,1,0)!=0)return;
     var config=Paths.Clone(cfg);
     Task.Run(()=>{
       UnitStatus[] v;
       string g="";
       try {
         var units=Paths.Units(config);
          v=new[]{Backend.Check(units[0],config),Backend.Check(units[1],config),config.QueryEnabled?Backend.Check(units[2],config):new UnitStatus{State="DISABLED"}};
         g=Backend.Gpu();
        } catch(Exception ex){Log("后台监控异常："+ex.Message);v=new[]{new UnitStatus(),new UnitStatus(),new UnitStatus{State="DISABLED"}};}
       try{
         if(!IsDisposed)BeginInvoke((Action)(()=>DrawData(v,g)));
       }catch{}
       finally{Interlocked.Exchange(ref poll,0);}
     });
   }
   void DrawData(UnitStatus[] statuses,string gpu){
     current=statuses;
     UpdateService(statuses[0],embstate,embpid,embport,embtime,cfg.EmbeddingPort);
     UpdateService(statuses[1],rerstate,rerpid,rerport,rertime,cfg.RerankerPort);
       UpdateService(statuses[2],querystate,querypid,queryport,querytime,cfg.QueryPort);
       bool a=statuses[0].State=="READY",b=statuses[1].State=="READY",q=!cfg.QueryEnabled||statuses[2].State=="READY";
       bool all=a&&b&&q,any=a||b||(cfg.QueryEnabled&&statuses[2].State=="READY");
       allstatus.Text=all?"✔  运行正常 · "+(cfg.QueryEnabled?3:2)+"个服务 READY":any?"◐  部分运行 · 可点击恢复":statuses.Any(x=>x.State=="LOADING")?"◌  模型正在加载":statuses.Any(x=>x.State=="FOREIGN")?"⚠  端口已被其他进程占用":"○  服务已停止";
       allstatus.ForeColor=all?Theme.Green:(any?Theme.Yellow:Theme.Muted);
       if(!configured){allstatus.Text="⚠ 尚未完成配置：请在「设置」中核对所需 GGUF（Query 为可选服务）";allstatus.ForeColor=Theme.Yellow;}
     if(!String.IsNullOrEmpty(gpu)){
       var parts=gpu.Split('|');
       if(parts.Length>=7){
         gpuname.Text=parts[0];
          float used,total,load;
         if(float.TryParse(parts[1],out used)&&float.TryParse(parts[2],out total)&&total>0){
           memBar.Portion=used/total;memBar.Invalidate();
           vramnumbers.Text=parts[1]+" / "+parts[2]+" MiB ("+Math.Round(used*100/total)+"%)";
         }
         freemem.Text=parts[3]+" MiB";
         if(float.TryParse(parts[4],out load)){utilBar.Portion=load/100f;utilBar.Invalidate();}
         gpuusage.Text=parts[4]+"%";temperature.Text=parts[5]+"°C";power.Text=parts[6]+" W";
       }
     }
     refresh.Text="● 最近刷新 "+DateTime.Now.ToString("HH:mm:ss");
   }
   void UpdateService(UnitStatus s,Label state,Label pid,Label port,Label span,int actualPort){
     state.Text=s.State;
      state.ForeColor=s.State=="READY"?Theme.Green:s.State=="DISABLED"?Theme.Muted:s.State=="STOPPED"?Theme.Yellow:Theme.Red;
     pid.Text=s.Pid>0?s.Pid.ToString():"—";
     port.Text=actualPort.ToString();
     if(s.Since> DateTime.MinValue){
       var d=DateTime.Now-s.Since;
       span.Text=d.Hours.ToString("00")+":"+d.Minutes.ToString("00")+":"+d.Seconds.ToString("00");
     }else span.Text="—";
   }
 }
}
