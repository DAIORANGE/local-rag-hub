using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using LocalRagHub;

class JobCrashHarness {
 [STAThread] static int Main(string[] args){
   try{return Run(args);}catch(Exception ex){
     Console.Error.WriteLine("CRASH_HARNESS_FAIL "+ex.GetBaseException().Message);
     return 1;
   }
 }
 static int Run(string[] args) {
   if(args.Length!=1)throw new Exception("Missing output marker");
   var cfg=Paths.Read();
   if(!String.IsNullOrEmpty(Paths.ConfigReadError)||cfg.EmbeddingPort!=15435
      ||!cfg.EmbeddingId.StartsWith("Fake-Embedding",StringComparison.Ordinal)
      ||!Path.GetFileName(cfg.ServerPath).Equals("llama-server.exe",StringComparison.OrdinalIgnoreCase)
      ||!Path.GetFileName(cfg.EmbeddingPath).Equals("fake-embedding.gguf",StringComparison.OrdinalIgnoreCase))
     throw new InvalidDataException("REFUSE_NON_SYNTHETIC_CONFIGURATION");
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   using(var form=new MainWindow()){
     form.ShowInTaskbar=false;form.Opacity=0;
     form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);
     var action=typeof(MainWindow).GetMethod("DoAction",BindingFlags.NonPublic|BindingFlags.Instance);
     bool dispatched=false;var start=DateTime.UtcNow;
     using(var clock=new System.Windows.Forms.Timer{Interval=150}){
       clock.Tick+=(s,e)=>{
         if((DateTime.UtcNow-start).TotalSeconds>22){clock.Stop();form.Close();return;}
         if(!dispatched){dispatched=true;action.Invoke(form,new object[]{"启动","Embedding"});return;}
         var pids=ModelProcessOwner.LivePids();
         if(pids.Length>0&&Backend.Check(Paths.Units(cfg)[0],cfg).State=="READY"){
           File.WriteAllText(args[0],pids[0].ToString());
           clock.Stop(); // The parent will force-kill this GUI process here.
         }
       };
       form.Shown+=(s,e)=>clock.Start();
       Application.Run(form);
     }
   }
   return 0;
 }
}