using System;
using System.Drawing;
using System.Windows.Forms;

namespace Bible2PPT
{
    /// <summary>
    /// 간단한 텍스트 입력 다이얼로그(DB 저장 이름 입력 등).
    /// </summary>
    public class NameInputForm : Form
    {
        public NameInputForm(string prompt, string initialText)
        {
            Text = prompt;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(360, 75);
            MinimumSize = new Size(360, 75);

            var textBox = new TextBox
            {
                Location = new Point(20, 28),
                Width = 190,
                Font = new Font("Microsoft YaHei", 10F)
            };
            textBox.Text = initialText;
            textBox.SelectAll();

            var okButton = new Button
            {
                Text = "확인",
                Location = new Point(235, 21),
                Size = new Size(50, 30),
                FlatStyle = FlatStyle.Flat
            };
            okButton.DialogResult = DialogResult.OK;

            var cancelButton = new Button
            {
                Text = "취소",
                Location = new Point(290, 21),
                Size = new Size(50, 30),
                FlatStyle = FlatStyle.Flat
            };
            cancelButton.DialogResult = DialogResult.Cancel;

            Controls.Add(textBox);
            Controls.Add(okButton);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
            okButton.FlatAppearance.BorderSize = 1;
            okButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(240, 240, 240);
            cancelButton.FlatAppearance.BorderSize = 1;
            cancelButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(240, 240, 240);
        }

        /// <summary>
        /// 입력된 텍스트를 반환한다.
        /// </summary>
        public string GetText()
        {
            foreach (Control control in Controls)
            {
                if (control is TextBox t)
                {
                    return t.Text;
                }
            }

            return string.Empty;
        }
    }
}
