using System;
using System.Linq;
using System.Windows.Forms;
using LocalRagHub;
class MonitorUiSmoke{
 [STAThread] static int Main(){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  using(var m=new MainWindow()){
   var cards=m.Controls.OfType<Card>().ToArray();
   var logCard=cards.FirstOrDefault(x=>x.Left==23&&x.Top==638);
   if(logCard==null)throw new Exception("LOG_CARD_NOT_FOUND");
   var buttons=logCard.Controls.OfType<ActionButton>().ToArray();
   if(buttons.Length!=2)throw new Exception("EXPECTED_2_MONITOR_BUTTONS");
   var capture=buttons.SingleOrDefault(x=>x.Text=="开启记录");
   var list=buttons.SingleOrDefault(x=>x.Text=="请求列表");
   if(capture==null||list==null)throw new Exception("MONITOR_BUTTON_MISSING");
   if(capture.Bounds.IntersectsWith(list.Bounds))throw new Exception("MONITOR_BUTTONS_OVERLAP");
   if(capture.Right>logCard.ClientSize.Width || list.Right>logCard.ClientSize.Width)throw new Exception("MONITOR_BUTTON_CLIPPED");
   var rect=logCard.Controls.OfType<Label>().First(x=>x.Text.Contains("运行日志")).Bounds;
   if(rect.IntersectsWith(list.Bounds)||rect.IntersectsWith(capture.Bounds))throw new Exception("LOG_HEADING_OVERLAP");
   if(logCard.Controls.OfType<RichTextBox>().Any())throw new Exception("LOG_TEXTBOX_UNEXPECTED_PARENT");
   Console.WriteLine("PASS main log card buttons, bounds, heading separation");
  }
  using(var m=new RequestMonitor(new HubConfig{EmbeddingPort=13435,RerankerPort=13436,QueryPort=13437},r=>{}))
  using(var history=new RequestListDialog(m)){
   if(history.Controls.OfType<ListView>().Count()!=1)throw new Exception("REQUEST_LIST_NOT_RENDERED");
   Console.WriteLine("PASS request list dialog");
  }
  var example=new RequestRecord{Id=1,When=DateTime.Now,Stage="Embedding",Source="来源未知",Group="未关联",
   Path="/v1/embeddings",RequestPreview="test",ResponsePreview="ok",Status=200};
  using(var detail=new RequestDetailDialog(example)){
   var tb=detail.Controls.OfType<TextBox>().Single();
   if(!tb.Text.Contains("来源未知")||!tb.Text.Contains("请求预览"))throw new Exception("DETAILS_MISSING");
   Console.WriteLine("PASS expandable details dialog");
  }
  return 0;
 }
}