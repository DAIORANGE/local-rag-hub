using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace LocalRagHub {
 public sealed class ManagedModelRecord {
   public int Pid,Port;
   public long StartUtcTicks;
   public string Role,Model,Alias,Server;
 }
 // Only model processes launched by this GUI are registered.
 // A Windows Job Object enforces kill-on-close even for abrupt GUI termination.
 public static class ModelProcessOwner {
   const uint KILL_ON_CLOSE=0x2000;
   static readonly object gate=new object();
   static readonly string journal=Path.Combine(Paths.AppDir,"managed-model-processes.json");
   static bool invalidJournal=false;
   static readonly List<ManagedModelRecord> owned=ReadJournal();
   static IntPtr job=IntPtr.Zero;

   [StructLayout(LayoutKind.Sequential)]
   struct BasicLimits {
     public long PerProcessUserTimeLimit,PerJobUserTimeLimit;
     public uint LimitFlags;
     public UIntPtr MinimumWorkingSetSize,MaximumWorkingSetSize;
     public uint ActiveProcessLimit;
     public UIntPtr Affinity;
     public uint PriorityClass,SchedulingClass;
   }
   [StructLayout(LayoutKind.Sequential)]
   struct IoCounters {
     public ulong ReadOperationCount,WriteOperationCount,OtherOperationCount,ReadTransferCount,WriteTransferCount,OtherTransferCount;
   }
   [StructLayout(LayoutKind.Sequential)]
   struct ExtendedLimits {
     public BasicLimits BasicLimitInformation;
     public IoCounters IoInfo;
     public UIntPtr ProcessMemoryLimit,JobMemoryLimit,PeakProcessMemoryUsed,PeakJobMemoryUsed;
   }
   [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
   static extern IntPtr CreateJobObject(IntPtr attributes,string name);
   [DllImport("kernel32.dll",SetLastError=true)]
   [return:MarshalAs(UnmanagedType.Bool)]
   static extern bool SetInformationJobObject(IntPtr handle,int infoClass,ref ExtendedLimits info,uint size);
   [DllImport("kernel32.dll",SetLastError=true)]
   [return:MarshalAs(UnmanagedType.Bool)]
   static extern bool AssignProcessToJobObject(IntPtr handle,IntPtr process);
   [DllImport("kernel32.dll",SetLastError=true)]
   [return:MarshalAs(UnmanagedType.Bool)]
   static extern bool CloseHandle(IntPtr handle);

   static List<ManagedModelRecord> ReadJournal(){
     if(!File.Exists(journal))return new List<ManagedModelRecord>();
     try{
       var list=new JavaScriptSerializer().Deserialize<List<ManagedModelRecord>>(File.ReadAllText(journal,Encoding.UTF8));
       if(list==null)throw new InvalidDataException("empty launch journal");
       return list;
     }catch(Exception ex){
       invalidJournal=true;
       Logger.Write("[ownership] launch journal unreadable: "+ex.Message);
       return new List<ManagedModelRecord>();
     }
   }
   static void Save(){
     if(invalidJournal)throw new InvalidDataException("managed-model-processes.json invalid: refusing to overwrite existing ownership journal");
     var tmp=journal+".tmp";
     File.WriteAllText(tmp,new JavaScriptSerializer().Serialize(owned),Encoding.UTF8);
     if(File.Exists(journal))File.Replace(tmp,journal,journal+".bak",true);
     else File.Move(tmp,journal);
   }
   static IntPtr GetJob(){
     if(job!=IntPtr.Zero)return job;
     IntPtr h=CreateJobObject(IntPtr.Zero,null);
     if(h==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"CreateJobObject failed");
     var limits=new ExtendedLimits();
     limits.BasicLimitInformation.LimitFlags=KILL_ON_CLOSE;
     if(!SetInformationJobObject(h,9,ref limits,(uint)Marshal.SizeOf(typeof(ExtendedLimits)))){
       int err=Marshal.GetLastWin32Error();
       CloseHandle(h);
       throw new Win32Exception(err,"SetInformationJobObject failed");
     }
     job=h;return job;
   }
   public static void Track(Process process,Unit unit,HubConfig config){
     // If ownership cannot be established, caller must kill its just-created process.
     lock(gate){
       if(invalidJournal)throw new InvalidDataException("Cannot launch: process ownership journal is unreadable");
       var item=new ManagedModelRecord{
         Pid=process.Id,StartUtcTicks=process.StartTime.ToUniversalTime().Ticks,
         Role=unit.Role,Model=Path.GetFullPath(unit.File),Alias=unit.Alias,Port=unit.Port,Server=Paths.Resolve(config.ServerPath)
       };
       if(!AssignProcessToJobObject(GetJob(),process.Handle))
         throw new Win32Exception(Marshal.GetLastWin32Error(),"Cannot attach child to kill-on-close job");
       owned.RemoveAll(x=>x.Pid==item.Pid);
       owned.Add(item);
       try{Save();}
       catch{owned.Remove(item);throw;}
     }
   }
   public static void Remove(int pid){
     lock(gate){
       if(owned.RemoveAll(x=>x.Pid==pid)>0)Save();
     }
   }
   public static bool Matches(ManagedModelRecord record){
     try{
       if(record==null||record.Pid<=0||record.StartUtcTicks<=0)return false;
       using(var p=Process.GetProcessById(record.Pid)){
         if(p.HasExited||p.StartTime.ToUniversalTime().Ticks!=record.StartUtcTicks)return false;
       }
       var unit=new Unit(record.Role,record.Model,record.Alias,record.Port,1);
       var c=new HubConfig{ServerPath=record.Server};
       return Backend.Owned(record.Pid,unit,c);
     }catch{return false;}
   }
   public static int[] LivePids(){
     lock(gate)return owned.Where(Matches).Select(x=>x.Pid).ToArray();
   }
   public static string[] StopVerified(){
     var errors=new List<string>();
     ManagedModelRecord[] records;
     lock(gate)records=owned.ToArray();
     foreach(var entry in records){
       try{
         if(!Matches(entry)){Remove(entry.Pid);continue;}
         using(var p=Process.GetProcessById(entry.Pid)){
           if(!Matches(entry))throw new InvalidOperationException("process PID or identity changed");
           p.Kill();
           if(!p.WaitForExit(7000))throw new TimeoutException("PID "+entry.Pid+" did not exit");
         }
         Logger.Write("[shutdown] verified owned "+entry.Role+" PID "+entry.Pid+" stopped");
         Remove(entry.Pid);
       }catch(Exception ex){errors.Add(entry.Role+" PID="+entry.Pid+": "+ex.Message);}
     }
     return errors.ToArray();
   }
   public static string[] Untracked(HubConfig config){
     var warnings=new List<string>();
     ManagedModelRecord[] records;
     lock(gate)records=owned.ToArray();
     foreach(var unit in Paths.Units(config)){
       var s=Backend.Check(unit,config);
       if(s.Pid<=0 || s.State=="FOREIGN")continue;
       if(!records.Any(x=>x.Pid==s.Pid&&Matches(x)))
         warnings.Add(unit.Role+" PID "+s.Pid+" (unverified launch origin)");
     }
     return warnings.ToArray();
   }
   public static void Release(){
     lock(gate){if(job!=IntPtr.Zero){CloseHandle(job);job=IntPtr.Zero;}}
   }
 }
}