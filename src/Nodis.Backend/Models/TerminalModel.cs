using CommunityToolkit.Mvvm.ComponentModel;
using ObservableCollections;
using XtermSharp;

namespace Nodis.Backend.Models;

public class TerminalModel
{
    public int Rows => Terminal.Rows;
    public int Cols => Terminal.Cols;
    public CharData this[int col, int row] => Terminal.Buffer.Lines[row][col];

    public Terminal Terminal { get; } = new();

    public ObservableDictionary<(int x, int y), TerminalText> Texts { get; } = new();

    public void Feed(byte[] data)
    {
        Terminal.Feed(data);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        Terminal.GetUpdateRange(out var lineStart, out var lineEnd);
        Terminal.ClearUpdateRange();

        var tb = Terminal.Buffer;
        for (var line = lineStart + tb.YDisp; line <= lineEnd + tb.YDisp; line++)
        {
            for (var cell = 0; cell < Terminal.Cols; cell++)
            {
                var charData = Terminal.Buffer.Lines[line][cell];
                var text = charData.Code == 0 ? " " : ((char)charData.Rune).ToString();

                if (Texts.TryGetValue((cell, line), out var terminalText))
                {
                    terminalText.Text = text;
                }
                else
                {
                    // var text2 = SetStyling(new TextObject(), charData);
                    // text2.Text = charData.Code == 0 ? " " : ((char)charData.Rune).ToString();
                    // Texts[(cell, line - tb.YDisp)] = text2;
                }
            }
        }
    }
}

public partial class TerminalText(
    string text,
    Color foreground,
    Color background,
    CharData charData
) : ObservableObject
{
    [ObservableProperty]
    public partial string Text { get; set; } = text;

    [ObservableProperty]
    public partial Color Foreground { get; set; } = foreground;

    [ObservableProperty]
    public partial Color Background { get; set; } = background;

    [ObservableProperty]
    public partial CharData CharData { get; set; } = charData;
}