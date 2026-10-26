using System;
using System.Drawing;
using System.Windows.Forms;
namespace CCSwitchUpdater {
 public sealed class NetworkSettingsForm : Form {
  readonly ComboBox mode=new ComboBox(); readonly TextBox address=new TextBox(); readonly TextBox hint=new TextBox(); readonly CheckBox mirrors=new CheckBox();
  public NetworkSettings Selected {get;private set;}
  public NetworkSettingsForm(NetworkSettings settings) {
   SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
   Text="网络设置 · 备用连接";Font=new Font("Microsoft YaHei UI",9F);ClientSize=new Size(620,460);MinimumSize=new Size(560,430);
   StartPosition=FormStartPosition.CenterParent;BackColor=Color.White;MinimizeBox=false;MaximizeBox=false;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Padding=new Padding(20)};
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   for(int i=0;i<7;i++)layout.RowStyles.Add(new RowStyle(i==4?SizeType.Percent:SizeType.AutoSize,i==4?100:0));
   Controls.Add(layout);
   layout.Controls.Add(new Label{Text="选择助手访问 GitHub 的方式",AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,0,0,10)},0,0);
   mode.Name="ConnectionMode";mode.DropDownStyle=ComboBoxStyle.DropDownList;mode.Items.AddRange(new object[]{"自动尝试（系统代理优先）","自动尝试（直连优先）","自动尝试（自定义代理优先）"});mode.Dock=DockStyle.Fill;mode.Margin=new Padding(0,0,0,12);layout.Controls.Add(mode,0,1);
   layout.Controls.Add(new Label{Text="HTTP 代理地址（仅自定义方式使用）",AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,0,0,5)},0,2);
   address.Name="ProxyAddress";address.Dock=DockStyle.Fill;address.Margin=new Padding(0,0,0,12);address.Text=settings.ProxyAddress ?? "";layout.Controls.Add(address,0,3);
   hint.Dock=DockStyle.Fill;hint.Multiline=true;hint.ReadOnly=true;hint.BorderStyle=BorderStyle.None;hint.BackColor=Color.White;hint.ScrollBars=ScrollBars.Vertical;hint.TabStop=false;hint.Text="首选线路失败后将自动尝试直连 / 系统连接。可填写已使用的 HTTP / Mixed 端口，例如 http://127.0.0.1:7890（仅示例，请以实际端口为准）。\r\n\r\n不会更改 Windows 或 Chrome 的代理，不读取 Cookie。代理会用于版本查询与安装包下载，目标仍是官方 HTTPS，保留证书和 SHA-256 校验。\r\n\r\n版本和摘要只取自官方。API 限流仍可走官方网页。附件先走官方及直连，再尝试勾选的备用下载代理；代理不能替代官方摘要。不支持 SOCKS 或代理账号密码。";hint.ForeColor=Color.FromArgb(80,94,115);layout.Controls.Add(hint,0,4);
   mirrors.Text="允许备用下载代理（gh-proxy.com、ghfast.top），始终核对官方 SHA-256";mirrors.AutoSize=true;mirrors.Dock=DockStyle.Fill;mirrors.Checked=settings.AllowDownloadMirrors;mirrors.Margin=new Padding(0,8,0,0);layout.Controls.Add(mirrors,0,5);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Margin=new Padding(0,12,0,0)};
   var save=new Button{Text="保存",AutoSize=true,MinimumSize=new Size(90,32)};var cancel=new Button{Text="取消",AutoSize=true,MinimumSize=new Size(90,32),DialogResult=DialogResult.Cancel};buttons.Controls.Add(save);buttons.Controls.Add(cancel);layout.Controls.Add(buttons,0,6);
   AcceptButton=save;CancelButton=cancel;
   mode.SelectedIndexChanged+=delegate{address.Enabled=mode.SelectedIndex==2;};mode.SelectedIndex=settings.Mode=="direct"?1:settings.Mode=="custom"?2:0;
   save.Click+=delegate{try{var chosen=new NetworkSettings{Mode=mode.SelectedIndex==1?"direct":mode.SelectedIndex==2?"custom":"system",ProxyAddress=address.Text.Trim(),AllowDownloadMirrors=mirrors.Checked};chosen.Validate();Selected=chosen;DialogResult=DialogResult.OK;Close();}catch(Exception e){MessageBox.Show(this,e.Message,"网络设置无效",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
   ResumeLayout(true);
  }
 }
}
