using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using LocalRagHub;
class MonitorSmoke {
 static HttpListener[] mocks;
 static int hits;
 static void Main(){
   mocks=new HttpListener[3];int[] ports={13435,13436,13437};
   try{
     for(int i=0;i<ports.Length;i++){
       mocks[i]=new HttpListener();
       mocks[i].Prefixes.Add("http://127.0.0.1:"+ports[i]+"/");
       mocks[i].Start();var l=mocks[i];
       Task.Run(()=>{while(l.IsListening){try{
         var c=l.GetContext();Interlocked.Increment(ref hits);
         using(var st=new StreamReader(c.Request.InputStream,Encoding.UTF8))st.ReadToEnd();
         byte[] res=Encoding.UTF8.GetBytes("{\"data\":[{\"id\":\"fake\"}],\"usage\":{\"total_tokens\":12}}");
         c.Response.ContentType="application/json";c.Response.ContentLength64=res.Length;
         c.Response.OutputStream.Write(res,0,res.Length);c.Response.Close();
       }catch{break;}}});
     }
     var cfg=new HubConfig{EmbeddingPort=13435,RerankerPort=13436,QueryPort=13437};
     using(var relay=new RequestMonitor(cfg,r=>Console.WriteLine("CAPTURE "+r.Summary))){
       relay.Start();
       Console.WriteLine("MONITOR_STARTED="+relay.Running);
       var first=Send(12437,"/src/TT-Nailong/v1/chat/completions","{\"model\":\"fake\",\"messages\":[{\"role\":\"user\",\"content\":\"test query\"}]}",true);
       var second=Send(12435,"/src/TT-Nailong/v1/embeddings","{\"model\":\"fake\",\"input\":\"test\"}",true);
       var third=Send(12436,"/src/TT-Nailong/v1/rerank","{\"query\":\"test\",\"documents\":[\"hello\",\"world\"]}",true);
       Console.WriteLine("RESPONSES="+first+" "+second+" "+third);
       for(int tries=0;tries<50 && relay.Snapshot().Length<3;tries++)Thread.Sleep(100);
       var records=relay.Snapshot();
       if(records.Length!=3)throw new Exception("WRONG_COUNT="+records.Length);
       if(records.Any(r=>r.Status!=200||r.Source!="TT-Nailong"||!r.ExplicitSource||!r.ExplicitTrace))throw new Exception("PROVENANCE_WRONG");
       if(records.Select(r=>r.Group).Distinct().Count()!=1)throw new Exception("TRACE_GROUP_WRONG");
       if(records.Select(r=>r.Stage).Distinct().Count()!=3)throw new Exception("MISSING_STAGE");
       if(records.Any(r=>r.RequestPreview=="（空）"||r.ResponsePreview=="（空）"))throw new Exception("MISSING_PREVIEW");
       if(hits!=3)throw new Exception("NOT_ALL_FORWARDED");
       var unknown=Send(12435,"/v1/embeddings","{\"input\":\"unknown\"}",false);
       Thread.Sleep(300);
       var r4=relay.Snapshot().Last();
       if(r4.ExplicitSource||r4.Group!="未关联")throw new Exception("UNKNOWN_SOURCE_MISLABELED");
       Console.WriteLine("PASS real local forwarding, all three stages, exact shared trace, honest unknown attribution");
       relay.Stop();
       if(relay.Running)throw new Exception("DID_NOT_STOP");
       Console.WriteLine("PASS STOP");
     }
   }finally{foreach(var l in mocks){try{if(l!=null)l.Close();}catch{}}}
 }
 static int Send(int port,string path,string json,bool trace) {
   var req=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:"+port+path);
   req.Proxy=null;req.Method="POST";req.ContentType="application/json";
   if(trace)req.Headers["X-RAG-Trace-ID"]="rp-test-001";
   byte[] b=Encoding.UTF8.GetBytes(json);req.ContentLength=b.Length;
   using(var stream=req.GetRequestStream())stream.Write(b,0,b.Length);
   using(var resp=(HttpWebResponse)req.GetResponse()){
     using(var reader=new StreamReader(resp.GetResponseStream())){
       if(!reader.ReadToEnd().Contains("\"fake\""))throw new Exception("BAD_UPSTREAM_RESPONSE");
     }
     return (int)resp.StatusCode;
   }
 }
}