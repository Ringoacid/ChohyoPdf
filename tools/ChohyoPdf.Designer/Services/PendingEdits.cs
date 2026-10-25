using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ChohyoPdf.Designer.Controls;

namespace ChohyoPdf.Designer.Services
{
    /// <summary>入力欄を離れたときに反映する欄で、入力途中の値をモデルに反映する。</summary>
    internal static class PendingEdits
    {
        /// <summary>
        /// ウィンドウでフォーカスのある欄（数値・フォント名・色・表のセル）の入力途中の値をモデルに反映する。
        /// 保存・出力・終了などの前に呼ぶ（メニューやショートカットキーでは入力欄からフォーカスが移らないため）。
        /// </summary>
        public static void Commit(Window window)
        {
            var focused = FocusManager.GetFocusedElement(window) as DependencyObject;
            if (focused == null && Keyboard.FocusedElement is DependencyObject keyboard && Window.GetWindow(keyboard) == window) focused = keyboard;
            if (focused == null) return;
            if (focused is TextBox box) box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            for (var d = focused; d != null; d = d is Visual ? VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            {
                switch (d)
                {
                    case FontComboBox font:
                        font.Commit();
                        return;
                    case FontSizeBox size:
                        size.Commit();
                        return;
                    case ComboBox combo:
                        combo.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                        return;
                    case ColorBox color:
                        color.CommitText();
                        return;
                    case DataGrid grid:
                        grid.CommitEdit(DataGridEditingUnit.Row, true);
                        return;
                    case Window:
                        return;
                }
            }
        }
    }
}
