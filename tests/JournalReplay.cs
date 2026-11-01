using System;
using LocalRagHub;
class JournalReplay {
 static int Main() {
   try{
     var errors=ModelProcessOwner.StopVerified();
     foreach(var s in errors)Console.WriteLine("ERROR "+s);
     Console.WriteLine("LIVE_OWNED="+ModelProcessOwner.LivePids().Length);
     if(errors.Length!=0||ModelProcessOwner.LivePids().Length!=0)return 1;
     return 0;
   }catch(Exception ex){Console.Error.WriteLine(ex.GetBaseException().Message);return 2;}
 }
}