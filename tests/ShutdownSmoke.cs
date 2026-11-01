using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using LocalRagHub;
class ShutdownSmoke{
 [STAThread] static int Main(){
   Application.EnableVisualStyles();
   Application.SetCompatibleTextRenderingDefault(false);
   var cfg=Paths.Read();
   var units=Paths.Units(cfg);
   if(units.Length!=2)throw new Exception("Expected 2 fake units");
   int started=0,wasReady=0;
   using(var form=new MainWindow()){
     var actions=typeof(MainWindow).GetMethod("DoAction",BindingFlags.Instance|BindingFlags.NonPublic);
     if(actions==null)throw new Exception("No DoAction method");
     var clock=new System.Windows.Forms.Timer{Interval=500};
     DateTime began=DateTime.Now;int phase=0;
     clock.Tick+=(s,e)=>{
       if((DateTime.Now-began).TotalSeconds>36){
         clock.Stop();
         throw new Exception("TIMEOUT_GUI_SHUTDOWN");
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
     };
     form.Shown+=(s,e)=>clock.Start();
     Application.Run(form);
     clock.Dispose();
   }
   if(started!=1)throw new Exception("NO_MANAGED_PROCESS_CREATED");
   if(Backend.LiveLaunchedPids().Length>0)throw new Exception("LINGERING_LLAMA_PID");
   var state=Backend.Check(units[0],cfg);
   if(state.State!="STOPPED")throw new Exception("SERVICE_STILL_RUNNING="+state.State);
   Console.WriteLine("PASS window closed; in-session model stopped; port released; live owned PIDs=0; wasReady="+wasReady);
   return 0;
 }
}