using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
class FakeLlama {
 static int Main(string[] args){
   int port=0;
   for(int i=0;i<args.Length-1;i++)if(args[i]=="--port")int.TryParse(args[i+1],out port);
   if(port<1024)return 22;
   bool slow=Array.IndexOf(args,"Fake-Embedding-Slow")>=0;
   var listener=new TcpListener(IPAddress.Loopback,port);
   listener.Start();
   try {
     while(true){
       using(var c=listener.AcceptTcpClient()){
         c.ReceiveTimeout=3000;
         using(var stream=c.GetStream()){
           var buf=new byte[4096];
           int n=stream.Read(buf,0,buf.Length);
           var payload=Encoding.UTF8.GetBytes(slow?"{\"status\":\"loading\"}":"{\"status\":\"ok\"}");
           var head=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: "+payload.Length+"\r\nConnection: close\r\n\r\n");
           stream.Write(head,0,head.Length);
           stream.Write(payload,0,payload.Length);
           stream.Flush();
         }
       }
     }
   }finally{listener.Stop();}
 }
}