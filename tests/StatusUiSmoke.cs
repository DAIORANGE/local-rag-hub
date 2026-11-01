using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LocalRagHub;
class StatusUiSmoke {
 [STAThread] static int Main(){
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   using(var app=new MainWindow()){
     app.Show();Application.DoEvents();
     var top=app.Controls.OfType<Card>().Single(x=>x.Top==15&&x.Left==23);
     var statusCard=top.Controls.OfType<Card>().Single(x=>x.Left==682);
     var status=statusCard.Controls.OfType<StatusText>().Single();
     var title=statusCard.Controls.OfType<Label>().Single(x=>x.Text.Contains("服务总状态"));
     if(status.Bounds.IntersectsWith(title.Bounds))throw new Exception("HEADER_TEXT_OVERLAP");
     if(status.Top<30||status.Bottom>statusCard.ClientSize.Height)throw new Exception("STATUS_OUTSIDE_CARD");
     status.Text="○  服务已停止";status.ForeColor=Theme.Green;status.Invalidate();status.Update();
     using(var b=new Bitmap(statusCard.Width,statusCard.Height)){
       statusCard.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));
       b.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"status-after.png"));
     }
     using(var b=new Bitmap(app.Width,app.Height)){
       app.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));
       b.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-layout.png"));
     }
     status.Text="正在完全退出，清理模型进程...";status.ForeColor=Theme.Yellow;status.Invalidate();status.Update();
     status.Text="○  服务已停止";status.ForeColor=Theme.Green;status.Invalidate();status.Update();
     if(statusCard.Controls.OfType<StatusText>().Count()!=1)throw new Exception("DUPLICATE_STATUS_CONTROL");
     Console.WriteLine("STATUS_UI_PASS opaque single-line status, separate heading, no overlapping status controls");
   }
   return 0;
 }
}