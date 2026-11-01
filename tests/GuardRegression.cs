using System;
using System.IO;
using System.Reflection;
using LocalRagHub;
class GuardRegression {
 static int failures=0;
 static void Test(string label,bool result) {
   Console.WriteLine((result?"PASS ":"FAIL ")+label);
   if(!result)failures++;
 }
 static bool IsManaged(string args,Unit u) {
   var t=typeof(Backend).GetMethod("MatchesLaunchCommand",BindingFlags.Static|BindingFlags.NonPublic);
   if(t==null)throw new Exception("MatchesLaunchCommand not found");
   return (bool)t.Invoke(null,new object[]{args,u});
 }
 static int Main(){
   string folder=@"C:\Models\query";
   string path=Path.Combine(folder,"Qwen_Qwen3.5-9B-Q5_K_M.gguf");
   var u=new Unit("Query",path,"Qwen3.5-9B",11437,1);
   string good="\"C:\\llama.cpp\\llama-server.exe\" -m \""+path+"\" --jinja --reasoning off --host 127.0.0.1 --port 11437 -c 8192 -np 1 -ngl 99 --alias \"Qwen3.5-9B\" --log-file \"query-server.log\"";
   Test("valid quoted command",IsManaged(good,u));
   Test("suffix of model filename",!IsManaged(good.Replace("Qwen_Qwen3.5-9B-Q5_K_M.gguf","Qwen_Qwen3.5-9B-Q5_K_M.gguf.old"),u));
   Test("model path appears only in log",!IsManaged(good.Replace("-m \""+path+"\"","-m \"C:\\Models\\other.gguf\"").Replace("query-server.log",path),u));
   Test("port suffix",!IsManaged(good.Replace("--port 11437","--port 114370"),u));
   Test("different port",!IsManaged(good.Replace("--port 11437","--port 11438"),u));
   Test("different bind address",!IsManaged(good.Replace("--host 127.0.0.1","--host 0.0.0.0"),u));
   Test("alias suffix",!IsManaged(good.Replace("--alias \"Qwen3.5-9B\"","--alias \"Qwen3.5-9B-backup\""),u));
   Test("multiple model flags",!IsManaged(good+" -m \""+path+"\"",u));
   Test("missing alias",!IsManaged(good.Replace("--alias \"Qwen3.5-9B\"",""),u));
   Test("missing model",!IsManaged(good.Replace("-m \""+path+"\"",""),u));
   var eb=new Unit("Embedding",@"C:\Models\embedding\Qwen3-Embedding-8B-Q4_K_M.gguf","Qwen3-Embedding-8B",11435,4);
   string ep="llama-server.exe -m \""+eb.File+"\" --embedding --pooling last --host 127.0.0.1 --port 11435 -c 16384 -np 4 -ngl 99 --alias \""+eb.Alias+"\"";
   Test("legacy embedding command",IsManaged(ep,eb));
   var legacy=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<HubConfig>("{\"EmbeddingPort\":11435,\"RerankerPort\":11436}");
   Test("legacy configuration remains opt-out",!legacy.QueryEnabled && legacy.EmbeddingPort==11435 && legacy.RerankerPort==11436);
   return failures==0?0:1;
 }
}