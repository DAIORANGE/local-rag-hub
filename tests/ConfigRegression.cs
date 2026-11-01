using System;
using System.IO;
using System.Text;
using LocalRagHub;
class ConfigRegression {
 static int fails=0;
 static void Check(string name,bool ok) { Console.WriteLine((ok?"PASS ":"FAIL ")+name); if(!ok)fails++; }
 static int Main() {
   byte[] original=File.ReadAllBytes(Paths.ConfigFile);
   try {
     string bad="{\"broken_json\": !!";
     File.WriteAllText(Paths.ConfigFile,bad,Encoding.UTF8);
     var fallback=Paths.Read();
     Check("corrupt config flagged",!String.IsNullOrWhiteSpace(Paths.ConfigReadError));
     bool refused=false;
     try { Paths.Save(fallback); }catch(InvalidOperationException){refused=true;}
     Check("cannot overwrite corrupt config",refused);
     Check("invalid bytes retained",File.ReadAllText(Paths.ConfigFile,Encoding.UTF8).Contains("broken_json"));
     File.Delete(Paths.ConfigFile);
     var config=Paths.Read();
     Check("after removing bad config error clears",String.IsNullOrEmpty(Paths.ConfigReadError));
     Check("no config opt-in remains off",config.QueryEnabled==false);
     File.WriteAllBytes(Paths.ConfigFile,original);
     config=Paths.Read();
     Paths.Save(config);
     Check("save config roundtrip",Paths.Read().EmbeddingPort==11435 && !Paths.Read().QueryEnabled);
     Check("saved file exists",File.Exists(Paths.ConfigFile));
   }finally{File.WriteAllBytes(Paths.ConfigFile,original);}
   return fails==0?0:1;
 }
}