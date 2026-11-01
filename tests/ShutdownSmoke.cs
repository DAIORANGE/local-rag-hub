using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using LocalRagHub;

// Synthetic integration harness only: never launch real models or open a visible
// window on the user's desktop. A failed test reports via stdout/exit code,
// not an unhandled WinForms exception dialog.
class ShutdownSmoke {
 [STAThread] static int Main() {
   try{return Run();}catch(Exception ex){
     Console.Error.WriteLine("SHUTDOWN_SMOKE_FAILED: "+ex.GetBaseException().Message);
     return 1;
   }
 }
 static int Run() {
   Application.EnableVisualStyles();
   Application.SetCompatibleTextRenderingDefault(false);
   var cfg=Paths.Read();
   if(!String.IsNullOrEmpty(Paths.ConfigReadError))throw new InvalidDataException("Test config is unreadable");
   string stage=Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
   string server=Path.GetFullPath(cfg.ServerPath??"");
   // Fail closed if launched from the desktop installation or against actual GGUFs.
   string serverDir=Path.GetDirectoryName(server);
   string embeddingName=Path.GetFileName(cfg.EmbeddingPath==null?"":cfg.EmbeddingPath);
   if(serverDir==null || !String.Equals(serverDir.TrimEnd('\\'),stage.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)
      || !String.Equals(Path.GetFileName(server),"llama-server.exe",StringComparison.OrdinalIgnoreCase)
      || !cfg.EmbeddingId.StartsWith("Fake-Embedding",StringComparison.Ordinal)
      || cfg.EmbeddingPort!=15435 || cfg.RerankerPort!=15436 || cfg.QueryEnabled
      || !String.Equals(embeddingName,"fake-embedding.gguf",StringComparison.OrdinalIgnoreCase))
     throw new InvalidOperationException("REFUSE_NON_SYNTHETIC_TEST_CONFIGURATION");
   var units=Paths.Units(cfg);
   if(units.Length!=2)throw new Exception("Expected two synthetic units");
   int started=0,wasReady=0;
   Exception tickError=null;
   using(var form=new MainWindow()) {
     form.ShowInTaskbar=false;
     form.Opacity=0;
     form.StartPosition=FormStartPosition.Manual;
     form.Location=new Point(-32000,-32000);
     var actions=typeof(MainWindow).GetMethod("DoAction",BindingFlags.Instance|BindingFlags.NonPublic);
     if(actions==null)throw new Exception("No DoAction method");
     var clock=new System.Windows.Forms.Timer{Interval=500};
     DateTime began=DateTime.Now;int phase=0;
     clock.Tick+=(s,e)=>{
       try {
         if((DateTime.Now-began).TotalSeconds>36){
           tickError=new TimeoutException("Synthetic shutdown test reached watchdog limit");
           clock.Stop();
           form.Close();
           return;
         }
         if(phase==0){phase=1;actions.Invoke(form,new object[]{"启动","Embedding"});return;}
         if(phase==1){
           var pid=Backend.LiveLaunchedPids();
           if(pid.Length>0){
             started=1;
             var status=Backend.Check(units[0],cfg);
             if(status.State=="READY")wasReady=1;
             phase=2;clock.Stop();form.Close();
           }
         }
       }catch(Exception ex){
         tickError=ex;
         clock.Stop();
         form.Close();
       }
     };
     form.Shown+=(s,e)=>clock.Start();
     try{Application.Run(form);}finally{clock.Dispose();}
   }
   if(tickError!=null)throw new Exception("Test timer failure",tickError);
   if(started!=1)throw new Exception("NO_SYNTHETIC_PROCESS_CREATED");
   if(Backend.LiveLaunchedPids().Length>0)throw new Exception("LINGERING_SYNTHETIC_PID");
   var state=Backend.Check(units[0],cfg);
   if(state.State!="STOPPED")throw new Exception("SYNTHETIC_SERVICE_STILL_RUNNING="+state.State);
   Console.WriteLine("PASS synthetic GUI shutdown; no foreground window, owned PIDs 0, test port released; wasReady="+wasReady);
   return 0;
 }
}