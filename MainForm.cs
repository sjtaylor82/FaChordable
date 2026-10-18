namespace FaChordable;

public sealed class MainForm : Form
{
    private readonly ComboBox keyBox = NewCombo("Choose a key");
    private readonly ComboBox familyBox = NewCombo("Choose a chord type");
    private readonly ComboBox chordBox = NewCombo("Choose a chord subtype");
    private readonly Button showButton = new() { Text = "&Show positions", AutoSize = true };
    private readonly Button updateButton = new() { Text = "Check for &updates", AutoSize = true };
    private readonly Button exitButton = new() { Text = "E&xit", AutoSize = true };
    private readonly Label statusLabel = new() { AutoSize = true, AccessibleRole = AccessibleRole.StaticText };
    private ChordCatalogue catalogue = new();
    private CancellationTokenSource? updateCancellation;

    public MainForm()
    {
        Text = "FaChordable — Accessible Guitar Chords";
        MinimumSize = new(620, 360);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(18), ColumnCount = 2, RowCount = 6 };
        layout.ColumnStyles.Add(new(SizeType.AutoSize));
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        AddRow(layout, 0, "&Key:", keyBox);
        AddRow(layout, 1, "Chord &type:", familyBox);
        AddRow(layout, 2, "Chord &subtype:", chordBox);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange([showButton, updateButton, exitButton]);
        layout.Controls.Add(buttons, 1, 3);
        layout.Controls.Add(statusLabel, 1, 4);
        Controls.Add(layout);

        keyBox.SelectedIndexChanged += (_, _) => SelectKey();
        familyBox.SelectedIndexChanged += (_, _) => SelectFamily();
        showButton.Click += (_, _) => ShowPositions();
        updateButton.Click += async (_, _) => await UpdateCatalogueAsync();
        exitButton.Click += (_, _) => { if (updateCancellation is null) Close(); else updateCancellation.Cancel(); };
        AcceptButton = showButton;
        CancelButton = exitButton;
        Shown += async (_, _) =>
        {
            await LoadCatalogueAsync();
            await CheckForUpdatesIfDueAsync();
        };
    }

    private static ComboBox NewCombo(string accessibleName) => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Fill,
        AccessibleName = accessibleName,
        TabStop = true
    };

    private static void AddRow(TableLayoutPanel layout, int row, string labelText, Control control)
    {
        var label = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left };
        label.UseMnemonic = true;
        label.TabIndex = row * 2;
        control.TabIndex = row * 2 + 1;
        label.AccessibleName = labelText.Replace("&", "");
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
        label.Click += (_, _) => control.Focus();
    }

    private async Task LoadCatalogueAsync()
    {
        try
        {
            catalogue = await CatalogueStore.LoadAsync();
            PopulateKeys();
            statusLabel.Text = catalogue.UpdatedAt == default
                ? "Using the bundled offline catalogue."
                : $"Chord data last updated {catalogue.UpdatedAt.ToLocalTime():d MMMM yyyy}.";
        }
        catch (Exception ex) { ShowError("The chord catalogue could not be loaded.", ex); }
    }

    private void PopulateKeys()
    {
        keyBox.DataSource = catalogue.Keys;
        if (keyBox.Items.Count > 0) keyBox.SelectedIndex = 0;
    }

    private async Task CheckForUpdatesIfDueAsync()
    {
        if (CatalogueStore.LastUpdateCheck is { } last && DateTimeOffset.Now - last < TimeSpan.FromDays(7)) return;
        try
        {
            var available = await new CatalogueUpdater().IsUpdateAvailableAsync(catalogue);
            CatalogueStore.RecordUpdateCheck();
            if (available) statusLabel.Text = "New chord entries are available. Choose Check for updates to download them.";
        }
        catch
        {
            // Startup must remain fast and usable offline; manual update reports detailed errors.
        }
    }

    private void SelectKey()
    {
        familyBox.DataSource = keyBox.SelectedItem is ChordKey key ? key.Families : null;
        if (familyBox.Items.Count > 0) familyBox.SelectedIndex = 0;
    }

    private void SelectFamily()
    {
        chordBox.DataSource = familyBox.SelectedItem is ChordFamily family ? family.Chords : null;
        if (chordBox.Items.Count > 0) chordBox.SelectedIndex = 0;
    }

    private void ShowPositions()
    {
        if (chordBox.SelectedItem is not Chord chord) return;
        if (chord.Positions.Count == 0)
        {
            MessageBox.Show(this, "FaChords currently provides no textual positions for this chord. Try checking for updates later.", chord.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var viewer = new PositionForm(chord);
        viewer.ShowDialog(this);
        chordBox.Focus();
    }

    private async Task UpdateCatalogueAsync()
    {
        updateCancellation = new();
        SetBusy(true);
        var progress = new Progress<string>(message => { statusLabel.Text = message; AccessibilityNotifyClients(AccessibleEvents.NameChange, statusLabel.TabIndex); });
        try
        {
            statusLabel.Text = "Checking for new chord entries…";
            var updater = new CatalogueUpdater();
            var updateAvailable = await updater.IsUpdateAvailableAsync(catalogue, updateCancellation.Token);
            CatalogueStore.RecordUpdateCheck();
            if (!updateAvailable)
            {
                statusLabel.Text = "The offline chord catalogue is already up to date.";
                MessageBox.Show(this, statusLabel.Text, "FaChordable update", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            statusLabel.Text = "New chord entries were found. Downloading the updated catalogue…";
            var updated = await updater.DownloadAsync(progress, updateCancellation.Token);
            await CatalogueStore.SaveAsync(updated);
            catalogue = updated;
            PopulateKeys();
            statusLabel.Text = $"Update complete: {catalogue.Keys.Count} keys and {catalogue.Keys.SelectMany(k => k.Families).SelectMany(f => f.Chords).Count()} chords.";
            MessageBox.Show(this, statusLabel.Text, "FaChordable update", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { statusLabel.Text = "Update cancelled; existing chord data was kept."; }
        catch (Exception ex) { ShowError("The update failed. Existing chord data was kept.", ex); }
        finally { SetBusy(false); updateCancellation.Dispose(); updateCancellation = null; }
    }

    private void SetBusy(bool busy)
    {
        keyBox.Enabled = familyBox.Enabled = chordBox.Enabled = showButton.Enabled = updateButton.Enabled = !busy;
        exitButton.Text = busy ? "&Cancel update" : "E&xit";
    }

    private void ShowError(string message, Exception ex)
    {
        statusLabel.Text = message;
        MessageBox.Show(this, $"{message}{Environment.NewLine}{Environment.NewLine}{ex.Message}", "FaChordable", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
