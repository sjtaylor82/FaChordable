using System.Diagnostics;

namespace FaChordable;

public sealed class PositionForm : Form
{
    private readonly Label positionHeading = new() { AutoSize = true, Dock = DockStyle.Fill, AccessibleRole = AccessibleRole.StaticText, Font = new Font(SystemFonts.DefaultFont.FontFamily, SystemFonts.DefaultFont.Size + 3, FontStyle.Bold) };
    private readonly AccessibleReadOnlyText instructions = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        DetectUrls = false,
        ScrollBars = RichTextBoxScrollBars.Vertical,
        AccessibleName = "Position instructions",
        AccessibleRole = AccessibleRole.Text,
        TabStop = true,
        WordWrap = true
    };
    private readonly Button previousButton = new() { Text = "&Previous position", AutoSize = true };
    private readonly Button nextButton = new() { Text = "&Next position", AutoSize = true };
    private readonly Button audioButton = new() { Text = "Play &audio", AutoSize = true };
    private readonly Button closeButton = new() { Text = "&Close", AutoSize = true, DialogResult = DialogResult.Cancel };
    private readonly Chord chord;
    private int positionIndex;

    public PositionForm(Chord chord)
    {
        this.chord = chord;
        Text = $"{chord.Name} positions";
        MinimumSize = new(680, 480);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(16), ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize));
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new(0, 10, 0, 0), FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        buttons.Controls.AddRange([previousButton, nextButton, audioButton, closeButton]);
        layout.Controls.Add(positionHeading, 0, 0);
        layout.Controls.Add(instructions, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);

        previousButton.Click += (_, _) => MovePosition(-1);
        nextButton.Click += (_, _) => MovePosition(1);
        audioButton.Click += (_, _) => PlayAudio();
        KeyDown += HandleNavigationKeys;
        CancelButton = closeButton;
        Shown += (_, _) => ShowPosition(0);
    }

    private void MovePosition(int change)
    {
        var target = Math.Clamp(positionIndex + change, 0, chord.Positions.Count - 1);
        if (target != positionIndex) ShowPosition(target);
    }

    private void ShowPosition(int index)
    {
        positionIndex = index;
        var position = chord.Positions[positionIndex];
        positionHeading.Text = $"{position.Name} of {chord.Positions.Count}";
        positionHeading.AccessibleName = positionHeading.Text;
        instructions.Text = position.AccessibleText;
        instructions.AccessibleDescription = "Read-only chord position instructions.";
        previousButton.Enabled = positionIndex > 0;
        nextButton.Enabled = positionIndex < chord.Positions.Count - 1;
        audioButton.Enabled = !string.IsNullOrWhiteSpace(position.AudioUrl);
        audioButton.AccessibleDescription = audioButton.Enabled ? $"Play audio for {position.Name}" : "Audio is unavailable for this position";
        instructions.Focus();
        instructions.SelectionStart = 0;
        instructions.SelectionLength = 0;
        instructions.ScrollToCaret();
        instructions.AnnounceChangedText();
    }

    private void HandleNavigationKeys(object? sender, KeyEventArgs e)
    {
        if ((e.Alt && e.KeyCode == Keys.P) || e.KeyCode == Keys.PageUp)
        {
            MovePosition(-1);
            e.Handled = e.SuppressKeyPress = true;
        }
        else if ((e.Alt && e.KeyCode == Keys.N) || e.KeyCode == Keys.PageDown)
        {
            MovePosition(1);
            e.Handled = e.SuppressKeyPress = true;
        }
    }

    private void PlayAudio()
    {
        var position = chord.Positions[positionIndex];
        if (position.AudioUrl is not { Length: > 0 } url) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, $"The audio could not be opened.{Environment.NewLine}{Environment.NewLine}{ex.Message}", "FaChordable", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}

internal sealed class AccessibleReadOnlyText : RichTextBox
{
    public void AnnounceChangedText()
    {
        AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
    }
}
