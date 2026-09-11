using System;
using System.Drawing;
using System.Windows.Forms;
namespace CCSwitchUpdater {
 public sealed class NetworkSettingsForm : Form {
  readonly ComboBox mode=new ComboBox(); readonly TextBox address=new TextBox(); readonly TextBox hint=new TextBox();
  public NetworkSettings Selected {get;private set;}
  public NetworkSettingsForm(NetworkSettings settings) {
   SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
   Text="网络设置 · 备用连接";Font=new Font("Microsoft YaHei UI",9F);ClientSize=new Size(590,380);MinimumSize=new Size(560,380);
   StartPosition=FormStartPosition.CenterParent;BackColor=Color.White;MinimizeBox=false;MaximizeBox=false;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(20)};
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   for(int i=0;i<6;i++)layout.RowStyles.Add(new RowStyle(i==4?SizeType.Percent:SizeType.AutoSize,i==4?100:0));
   Controls.Add(layout);
   layout.Controls.Add(new Label{Text="选择助手访问 GitHub 的方式",AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,0,0,10)},0,0);
   mode.Name="ConnectionMode";mode.DropDownStyle=ComboBoxStyle.DropDownList;mode.Items.AddRange(new object[]{"系统代理（默认）","直连（不使用系统代理）","自定义 HTTP 代理"});mode.Dock=DockStyle.Fill;mode.Margin=new Padding(0,0,0,12);layout.Controls.Add(mode,0,1);
   layout.Controls.Add(new Label{Text="HTTP 代理地址（仅自定义方式使用）",AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,0,0,5)},0,2);
   address.Name="ProxyAddress";address.Dock=DockStyle.Fill;address.Margin=new Padding(0,0,0,12);address.Text=settings.ProxyAddress ?? "";layout.Controls.Add(address,0,3);
   hint.Dock=DockStyle.Fill;hint.Multiline=true;hint.ReadOnly=true;hint.BorderStyle=BorderStyle.None;hint.BackColor=Color.White;hint.ScrollBars=ScrollBars.Vertical;hint.TabStop=false;hint.Text="可填写你已使用的可信代理软件的 HTTP / Mixed 端口，例如 http://127.0.0.1:7890（仅示例，请以实际端口为准）。\r\n\r\n不会更改 Windows 或 Chrome 的代理，不读取 Cookie。代理会用于版本查询与安装包下载，目标仍是官方 HTTPS，保留证书和 SHA-256 校验。\r\n\r\n不支持 SOCKS、带账号密码的代理或 URL 拼接反代。遇到明确限流请等待恢复时间，切线路不会清除本次等待。";hint.ForeColor=Color.FromArgb(80,94,115);layout.Controls.Add(hint,0,4);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Margin=new Padding(0,12,0,0)};
   var save=new Button{Text="保存",AutoSize=true,MinimumSize=new Size(90,32)};var cancel=new Button{Text="取消",AutoSize=true,MinimumSize=new Size(90,32),DialogResult=DialogResult.Cancel};buttons.Controls.Add(save);buttons.Controls.Add(cancel);layout.Controls.Add(buttons,0,5);
   AcceptButton=save;CancelButton=cancel;
   mode.SelectedIndexChanged+=delegate{address.Enabled=mode.SelectedIndex==2;};mode.SelectedIndex=settings.Mode=="direct"?1:settings.Mode=="custom"?2:0;
   save.Click+=delegate{try{var chosen=new NetworkSettings{Mode=mode.SelectedIndex==1?"direct":mode.SelectedIndex==2?"custom":"system",ProxyAddress=address.Text.Trim()};chosen.Validate();Selected=chosen;DialogResult=DialogResult.OK;Close();}catch(Exception e){MessageBox.Show(this,e.Message,"网络设置无效",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
   ResumeLayout(true);
  }
 }
}
