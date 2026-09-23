using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using BdoPin.Core;
using BdoPin.Localization;
namespace BdoPin.UI
{
    internal sealed class AboutForm : Form
    {
        private readonly Font titleFont, headingFont;
        private readonly Image profile;
        internal AboutForm(string language)
        {
            var t = new AboutText(language);
            Text = t.Pick("BdoPin 정보", "About BdoPin");
            Font = SystemFonts.MessageBoxFont;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(510, 500);
            titleFont = new Font(Font.FontFamily, 15, FontStyle.Bold);
            headingFont = new Font(Font, FontStyle.Bold);
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("BdoPin.Icon"))
            using (var original = new Icon(stream)) Icon = (Icon)original.Clone();
            using (var stream = assembly.GetManifestResourceStream("BdoPin.Profile"))
            using (var original = Image.FromStream(stream)) profile = new Bitmap(original);
            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(20, 15, 20, 14),
                ColumnCount = 1, RowCount = 9
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 32, 26, 174, 70, 27, 43, 38, 25, 36 })
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            root.Controls.Add(Label(VersionInfo.Product, titleFont, ContentAlignment.MiddleCenter), 0, 0);
            root.Controls.Add(Label(t.Pick("버전 ", "Version ") + VersionInfo.Display, Font, ContentAlignment.TopCenter), 0, 1);
            var identity = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 5, 0, 10) };
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            identity.Controls.Add(new PictureBox {
                Image = profile, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 16, 0), AccessibleName = "Eltax profile image", TabStop = false
            }, 0, 0);
            var details = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(0) };
            foreach (int height in new[] { 57, 32, 33, 20 }) details.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            details.Controls.Add(Label(t.Pick("검은사막 CPU Affinity 유틸리티", "Black Desert CPU Affinity Utility"), headingFont), 0, 0);
            details.Controls.Add(Label(t.Pick("개발자: Eltax", "Developer: Eltax"), Font), 0, 1);
            details.Controls.Add(LinkButton("GitHub", () => AboutLinks.Open(this, AboutLinks.GitHub, t.Korean)), 0, 2);
            identity.Controls.Add(details, 1, 0); root.Controls.Add(identity, 0, 2);
            root.Controls.Add(Label(t.Pick(
                "BdoPin은 BlackDesert64.exe의 CPU Affinity와 프로세스 우선순위를 간편하게 설정하기 위한 Windows 유틸리티입니다.",
                "BdoPin is a lightweight Windows utility for configuring CPU affinity and process priority for BlackDesert64.exe."), Font), 0, 3);
            root.Controls.Add(Label("Komori Edition", headingFont), 0, 4);
            root.Controls.Add(Label(t.Pick("고마운 사람", "Special thanks") + Environment.NewLine + "Hikimori Neko", Font), 0, 5);
            root.Controls.Add(LinkButton(t.Pick("Hikimori Neko 치지직", "Hikimori Neko CHZZK"),
                () => AboutLinks.Open(this, AboutLinks.Chzzk, t.Korean)), 0, 6);
            root.Controls.Add(Label(t.Pick("라이선스: Apache License 2.0", "License: Apache License 2.0"), Font), 0, 7);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
            footer.Controls.Add(Label("Copyright © 2026 Eltax", Font), 0, 0);
            var ok = new Button { Text = t.Pick("확인", "OK"), DialogResult = DialogResult.OK, Dock = DockStyle.Fill, UseVisualStyleBackColor = true };
            footer.Controls.Add(ok, 1, 0); root.Controls.Add(footer, 0, 8);
            Controls.Add(root); AcceptButton = ok; CancelButton = ok;
        }
        private static Label Label(string text, Font font, ContentAlignment alignment = ContentAlignment.MiddleLeft)
        {
            return new Label { Text = text, Font = font, Dock = DockStyle.Fill, TextAlign = alignment, AutoEllipsis = false, UseMnemonic = false, Margin = new Padding(0) };
        }
        private static Button LinkButton(string text, Action clicked)
        {
            var button = new Button { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, UseVisualStyleBackColor = true, Padding = new Padding(9, 2, 9, 2) };
            button.Click += delegate { clicked(); };
            return button;
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) { profile.Dispose(); titleFont.Dispose(); headingFont.Dispose(); if (Icon != null) Icon.Dispose(); }
        }
    }
}
