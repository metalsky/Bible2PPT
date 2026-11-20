using System.Drawing;
using System.Windows.Forms;

namespace Bible2PPT;

/// <summary>
/// 라이트(화이트) 테마를 WinForms 컨트롤 트리 전체에 적용한다.
/// </summary>
public static class LightTheme
{
    // 라이트 테마 색상
    public static readonly Color Background = Color.FromArgb(248, 248, 248);
    public static readonly Color Surface = Color.FromArgb(255, 255, 255);
    public static readonly Color SurfaceLight = Color.FromArgb(244, 244, 244);
    // 밝은 테마는 요청대로 글자를 검정색으로 사용한다(흰 배경에 잘 보이도록).
    public static readonly Color Text = Color.Black;
    public static readonly Color Muted = Color.FromArgb(60, 60, 60);
    public static readonly Color Accent = Color.FromArgb(0, 120, 212);
    public static readonly Color Selection = Color.FromArgb(217, 233, 255);
    public static readonly Color SelectionText = Color.FromArgb(32, 32, 32);
    public static readonly Color HeaderBg = Color.FromArgb(234, 234, 234);
    public static readonly Color FieldBorder = Color.FromArgb(190, 190, 190);

    /// <summary>
    /// 컨트롤 트리 전체에 라이트 테마를 재귀적으로 적용한다.
    /// </summary>
    public static void Apply(Control root)
    {
        ApplyRecursive(root);
    }

    private static void ApplyRecursive(Control control)
    {
        ApplyControl(control);
        foreach (Control child in control.Controls)
        {
            ApplyRecursive(child);
        }
    }

    private static void ApplyStatusStrip(StatusStrip ss)
    {
        ss.BackColor = SurfaceLight;
        ss.ForeColor = Text;

        foreach (ToolStripItem item in ss.Items)
        {
            item.ForeColor = Text;
            item.BackColor = SurfaceLight;
        }
    }

    private static void ApplyControl(Control control)
    {
        switch (control)
        {
            case Form form:
                form.BackColor = Background;
                form.ForeColor = Text;
                break;
            case StatusStrip ss:
                ApplyStatusStrip(ss);
                break;
            case Label label:
                label.BackColor = Color.Transparent;
                label.ForeColor = Text;
                break;
            case Button button:
                ApplyButton(button);
                break;
            case ComboBox comboBox:
                comboBox.BackColor = Surface;
                comboBox.ForeColor = Text;
                break;
            case TextBox textBox:
                textBox.BackColor = Surface;
                textBox.ForeColor = Text;
                break;
            case ListView listView:
                listView.BackColor = Surface;
                listView.ForeColor = Text;
                break;
            case ListBox listBox:
                listBox.BackColor = Surface;
                listBox.ForeColor = Text;
                break;
            case DataGridView dataGrid:
                ApplyDataGridView(dataGrid);
                break;
            case TableLayoutPanel tpl:
                tpl.BackColor = Background;
                break;
            case FlowLayoutPanel fpl:
                fpl.BackColor = Background;
                break;
            case Panel panel:
                panel.BackColor = Surface;
                break;
            case SplitContainer split:
                split.BackColor = Background;
                break;
            case ContainerControl cc:
                cc.BackColor = Surface;
                break;
            case CheckBox checkBox:
                checkBox.ForeColor = Text;
                break;
        }
    }

    private static void ApplyButton(Control control)
    {
        if (control is Button button)
        {
            button.BackColor = SurfaceLight;
            button.ForeColor = Text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = FieldBorder;
            button.FlatAppearance.MouseOverBackColor = Surface;
            button.FlatAppearance.MouseDownBackColor = Surface;
            button.FlatAppearance.CheckedBackColor = Surface;
        }
    }

    private static void ApplyDataGridView(DataGridView dataGrid)
    {
        dataGrid.BackgroundColor = Surface;
        dataGrid.ColumnHeadersDefaultCellStyle.BackColor = HeaderBg;
        dataGrid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        // 행 번호 열도 테마에 따라 색을 정한다 (라이트/다크 공통)
        dataGrid.RowHeadersDefaultCellStyle.BackColor = Surface;
        dataGrid.RowHeadersDefaultCellStyle.ForeColor = Text;
        dataGrid.RowHeadersDefaultCellStyle.SelectionBackColor = Selection;
        dataGrid.RowHeadersDefaultCellStyle.SelectionForeColor = SelectionText;
        dataGrid.DefaultCellStyle.ForeColor = Text;
        dataGrid.DefaultCellStyle.BackColor = Surface;
        dataGrid.DefaultCellStyle.SelectionBackColor = Selection;
        dataGrid.DefaultCellStyle.SelectionForeColor = SelectionText;
    }
}
