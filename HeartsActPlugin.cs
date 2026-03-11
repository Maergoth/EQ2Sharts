using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using Advanced_Combat_Tracker;

namespace HeartsActPlugin
{
    public class HeartsActPlugin : UserControl, IActPluginV1
    {
        private readonly LinkedList<string> PlayerQueue = new LinkedList<string>();
        private readonly HashSet<string> PlayerSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object Sync = new object();

        private Regex ChannelMsgRegex;
        private Regex[] EnqueuePatterns = Array.Empty<Regex>();
        private Regex[] DequeuePatterns = Array.Empty<Regex>();

        private Label LblStatus;
        private string SettingsFile;
        private SettingsSerializer XmlSettings;

        // UI controls - plugin tab
        private TextBox TxtEnqueuePatterns;
        private TextBox TxtDequeuePatterns;
        private TextBox TxtChannelName;
        private TextBox TxtLog;
        private CheckBox ChkOverlayVisible;
        private CheckBox ChkClickThrough;
        private TrackBar TrkOpacity;
        private Label LblOpacityValue;
        private TrackBar TrkFontScale;
        private Label LblFontScaleValue;
        private TextBox TxtOverlayBounds;
        private Button BtnClearQueue;
        private Button BtnBgColor;
        private Button BtnFgColor;
        private TextBox TxtBgColor;
        private TextBox TxtFgColor;

        // Overlay window
        private QueueOverlayForm Overlay;

        public HeartsActPlugin() => InitializeComponent();

        private void InitializeComponent()
        {
            SuspendLayout();

            // --- Main two-column layout ---
            var mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(4)
            };
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            mainTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // === Left column: pattern settings ===
            var leftPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(2)
            };

            var lblChannel = new Label { Text = "Channel Name:", AutoSize = true };
            TxtChannelName = new TextBox
            {
                Name = "txtChannelName",
                Size = new Size(280, 20),
                Text = "Raid"
            };

            var lblEnqueue = new Label { Text = "Enqueue Patterns (regex, one per line):", AutoSize = true };
            TxtEnqueuePatterns = new TextBox
            {
                Name = "txtEnqueuePatterns",
                Size = new Size(280, 60),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Text = "^HEARTS PLZ$\r\n^HEARTS PLX$\r\n^HEARTS PLOX$"
            };

            var lblDequeue = new Label { Text = "Dequeue Patterns (regex w/ capture group, one per line):", AutoSize = true };
            TxtDequeuePatterns = new TextBox
            {
                Name = "txtDequeuePatterns",
                Size = new Size(280, 60),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Text = "^HEARTS TO ([^\\s]+)$\r\n^MY HEART TO ([^\\s]+)$"
            };

            BtnClearQueue = new Button
            {
                Text = "Clear Queue",
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 0)
            };

            leftPanel.Controls.Add(lblChannel);
            leftPanel.Controls.Add(TxtChannelName);
            leftPanel.Controls.Add(lblEnqueue);
            leftPanel.Controls.Add(TxtEnqueuePatterns);
            leftPanel.Controls.Add(lblDequeue);
            leftPanel.Controls.Add(TxtDequeuePatterns);
            leftPanel.Controls.Add(BtnClearQueue);

            // === Right column: overlay settings ===
            var rightPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(2)
            };

            var lblOverlay = new Label
            {
                Text = "Overlay Settings:",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold)
            };

            ChkOverlayVisible = new CheckBox
            {
                Name = "chkOverlayVisible",
                Text = "Show Overlay",
                AutoSize = true,
                Checked = true
            };

            ChkClickThrough = new CheckBox
            {
                Name = "chkClickThrough",
                Text = "Click-Through (no titlebar)",
                AutoSize = true,
                Checked = false
            };

            // Opacity: label + trackbar in a row
            var opacityRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0)
            };
            opacityRow.Controls.Add(new Label { Text = "Opacity:", AutoSize = true, Margin = new Padding(0, 6, 0, 0) });
            TrkOpacity = new TrackBar
            {
                Name = "trkOpacity",
                Size = new Size(160, 30),
                Minimum = 10,
                Maximum = 100,
                Value = 90,
                TickFrequency = 10,
                SmallChange = 5,
                LargeChange = 10
            };
            opacityRow.Controls.Add(TrkOpacity);
            LblOpacityValue = new Label { Text = "90%", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            opacityRow.Controls.Add(LblOpacityValue);

            // Font scale: label + trackbar in a row
            var fontScaleRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0)
            };
            fontScaleRow.Controls.Add(new Label { Text = "Font Scale:", AutoSize = true, Margin = new Padding(0, 6, 0, 0) });
            TrkFontScale = new TrackBar
            {
                Name = "trkFontScale",
                Size = new Size(160, 30),
                Minimum = 50,
                Maximum = 300,
                Value = 100,
                TickFrequency = 25,
                SmallChange = 10,
                LargeChange = 25
            };
            fontScaleRow.Controls.Add(TrkFontScale);
            LblFontScaleValue = new Label { Text = "100%", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            fontScaleRow.Controls.Add(LblFontScaleValue);

            // Colors: bg + fg in a row
            var colorRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0)
            };
            colorRow.Controls.Add(new Label { Text = "Background:", AutoSize = true, Margin = new Padding(0, 4, 0, 0) });
            BtnBgColor = new Button
            {
                Text = "",
                Size = new Size(30, 22),
                BackColor = Color.FromArgb(30, 30, 30),
                FlatStyle = FlatStyle.Flat
            };
            colorRow.Controls.Add(BtnBgColor);
            colorRow.Controls.Add(new Label { Text = "Text:", AutoSize = true, Margin = new Padding(6, 4, 0, 0) });
            BtnFgColor = new Button
            {
                Text = "",
                Size = new Size(30, 22),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            colorRow.Controls.Add(BtnFgColor);

            rightPanel.Controls.Add(lblOverlay);
            rightPanel.Controls.Add(ChkOverlayVisible);
            rightPanel.Controls.Add(ChkClickThrough);
            rightPanel.Controls.Add(opacityRow);
            rightPanel.Controls.Add(fontScaleRow);
            rightPanel.Controls.Add(colorRow);

            mainTable.Controls.Add(leftPanel, 0, 0);
            mainTable.Controls.Add(rightPanel, 1, 0);

            // === Bottom row: activity log (spans both columns) ===
            var logPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            var lblLog = new Label { Text = "Activity Log:", AutoSize = true, Dock = DockStyle.Top };
            TxtLog = new TextBox
            {
                Name = "txtLog",
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };
            logPanel.Controls.Add(TxtLog);
            logPanel.Controls.Add(lblLog);
            mainTable.Controls.Add(logPanel, 0, 1);
            mainTable.SetColumnSpan(logPanel, 2);

            #if DEBUG
            mainTable.RowStyles[1] = new RowStyle(SizeType.Absolute, 160);
            #else
            mainTable.RowStyles[1] = new RowStyle(SizeType.Absolute, 0);
            logPanel.Visible = false;
            #endif

            // Hidden controls for settings persistence
            TxtOverlayBounds = new TextBox { Name = "txtOverlayBounds", Visible = false };
            TxtBgColor = new TextBox { Name = "txtBgColor", Text = "30,30,30", Visible = false };
            TxtFgColor = new TextBox { Name = "txtFgColor", Text = "255,255,255", Visible = false };
            Controls.Add(TxtOverlayBounds);
            Controls.Add(TxtBgColor);
            Controls.Add(TxtFgColor);

            Controls.Add(mainTable);

            Name = "HeartsActPlugin";
            Size = new Size(580, 400);

            ResumeLayout(false);
            PerformLayout();
        }

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            LblStatus = pluginStatusText;
            pluginScreenSpace.Controls.Add(this);
            Dock = DockStyle.Fill;

            SettingsFile = Path.Combine(
                ActGlobals.oFormActMain.AppDataFolder.FullName,
                "Config\\HeartsActPlugin.config.xml");

            XmlSettings = new SettingsSerializer(this);
            LoadSettings();

            RebuildPatterns();

            TxtChannelName.TextChanged += (s, e) => RebuildPatterns();
            TxtEnqueuePatterns.TextChanged += (s, e) => RebuildPatterns();
            TxtDequeuePatterns.TextChanged += (s, e) => RebuildPatterns();

            ActGlobals.oFormActMain.OnLogLineRead += OnLogLineRead;

            LblOpacityValue.Text = $"{TrkOpacity.Value}%";
            LblFontScaleValue.Text = $"{TrkFontScale.Value}%";

            Overlay = new QueueOverlayForm();
            ApplyOverlayBounds();
            ApplyOverlayColors();
            Overlay.SetClickThrough(ChkClickThrough.Checked);
            Overlay.Opacity = TrkOpacity.Value / 100.0;
            Overlay.SetFontScale(TrkFontScale.Value / 100f);
            if (ChkOverlayVisible.Checked)
                Overlay.Show();

            ChkOverlayVisible.CheckedChanged += (s, e) =>
            {
                if (ChkOverlayVisible.Checked)
                    Overlay.Show();
                else
                    Overlay.Hide();
            };

            ChkClickThrough.CheckedChanged += (s, e) =>
            {
                Overlay.SetClickThrough(ChkClickThrough.Checked);
            };

            TrkOpacity.ValueChanged += (s, e) =>
            {
                var pct = TrkOpacity.Value;
                LblOpacityValue.Text = $"{pct}%";
                Overlay.Opacity = pct / 100.0;
            };

            TrkFontScale.ValueChanged += (s, e) =>
            {
                var pct = TrkFontScale.Value;
                LblFontScaleValue.Text = $"{pct}%";
                Overlay.SetFontScale(pct / 100f);
            };

            BtnBgColor.Click += (s, e) =>
            {
                using (var dlg = new ColorDialog { Color = BtnBgColor.BackColor })
                {
                    if (dlg.ShowDialog() != DialogResult.OK)
                        return;

                    BtnBgColor.BackColor = dlg.Color;
                    TxtBgColor.Text = $"{dlg.Color.R},{dlg.Color.G},{dlg.Color.B}";
                    ApplyOverlayColors();
                }
            };

            BtnFgColor.Click += (s, e) =>
            {
                using (var dlg = new ColorDialog { Color = BtnFgColor.BackColor })
                {
                    if (dlg.ShowDialog() != DialogResult.OK)
                        return;

                    BtnFgColor.BackColor = dlg.Color;
                    TxtFgColor.Text = $"{dlg.Color.R},{dlg.Color.G},{dlg.Color.B}";
                    ApplyOverlayColors();
                }
            };

            BtnClearQueue.Click += (s, e) =>
            {
                lock (Sync)
                {
                    PlayerQueue.Clear();
                    PlayerSet.Clear();
                }

                AppendLog("Queue cleared.");
                RefreshQueueDisplay();
            };

            LblStatus.Text = "Hearts Plugin Started";
        }

        public void DeInitPlugin()
        {
            ActGlobals.oFormActMain.OnLogLineRead -= OnLogLineRead;
            SaveOverlayBounds();
            SaveSettings();

            if (Overlay != null)
            {
                Overlay.Close();
                Overlay.Dispose();
                Overlay = null;
            }

            LblStatus.Text = "Hearts Plugin Exited";
        }

        private void OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            if (isImport)
                return;

            var line = logInfo.logLine;

            var channelRegex = ChannelMsgRegex;
            if (channelRegex == null)
                return;
            
            // Fast pre-filter before regex
            if (!line.Contains(" tell") || !line.Contains(TxtChannelName.Text))
                return;

            var channelMatch = channelRegex.Match(line);
            if (!channelMatch.Success)
                return;

            var sender = channelMatch.Groups[1].Value;
            var content = channelMatch.Groups[2].Value;

            AppendLog($"[DEBUG] Channel message - sender=\"{sender}\", content=\"{content}\"");

            // Check enqueue patterns - sender gets enqueued
            var enqueuePatterns = EnqueuePatterns;
            foreach (var pattern in enqueuePatterns)
                if (pattern.IsMatch(content))
                {
                    AppendLog($"[DEBUG] Enqueue pattern matched: /{pattern}/ - enqueueing \"{sender}\"");
                    EnqueuePlayer(sender);
                    return;
                }

            // Check dequeue patterns - group 1 captures the player name to dequeue
            var dequeuePatterns = DequeuePatterns;
            foreach (var pattern in dequeuePatterns)
            {
                var dequeueMatch = pattern.Match(content);
                if (dequeueMatch.Success)
                {
                    if ((dequeueMatch.Groups.Count > 1) && dequeueMatch.Groups[1].Success)
                    {
                        var playerName = dequeueMatch.Groups[1].Value.Trim();
                        AppendLog($"[DEBUG] Dequeue pattern matched: /{pattern}/ - dequeueing \"{playerName}\"");
                        DequeuePlayer(playerName);
                    }
                    else
                        AppendLog($"[DEBUG] Dequeue pattern matched: /{pattern}/ but no capture group found. Add a (capture group) for the player name.");

                    return;
                }
            }

            AppendLog($"[DEBUG] Content \"{content}\" matched no enqueue or dequeue pattern.");
        }

        private void EnqueuePlayer(string name)
        {
            lock (Sync)
            {
                if (!PlayerSet.Add(name))
                {
                    AppendLog($"Player \"{name}\" is already in the queue.");
                    return;
                }

                PlayerQueue.AddLast(name);

                AppendLog($"Player \"{name}\" added to queue (position {PlayerQueue.Count}).");
            }

            RefreshQueueDisplay();
        }

        private void DequeuePlayer(string playerName)
        {
            lock (Sync)
            {
                if (!PlayerSet.Remove(playerName))
                {
                    AppendLog($"Player \"{playerName}\" is not in the queue.");
                    return;
                }

                var match = PlayerQueue.FirstOrDefault(n => string.Equals(n, playerName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    PlayerQueue.Remove(match);

                AppendLog($"Player \"{playerName}\" dequeued. Queue size: {PlayerQueue.Count}.");
            }

            RefreshQueueDisplay();
        }

        private void RebuildPatterns()
        {
            EnqueuePatterns = ParseRegexLines(TxtEnqueuePatterns.Text, "Enqueue");
            DequeuePatterns = ParseRegexLines(TxtDequeuePatterns.Text, "Dequeue");

            var channel = (TxtChannelName.Text ?? string.Empty).Trim();
            if (channel.Length > 0)
            {
                var escaped = Regex.Escape(channel);
                var channelPattern = $"([^\\s]+?) tells? {escaped} \\(\\d+\\), \"(.+?)\"";

                ChannelMsgRegex = new Regex(channelPattern, RegexOptions.Compiled);

                AppendLog($"[DEBUG] Patterns rebuilt - channel=\"{channel}\"");
                AppendLog($"[DEBUG]   Channel regex: {channelPattern}");
                AppendLog($"[DEBUG]   Enqueue patterns: {EnqueuePatterns.Length}");
                AppendLog($"[DEBUG]   Dequeue patterns: {DequeuePatterns.Length}");
            }
            else
            {
                ChannelMsgRegex = null;
                AppendLog("[DEBUG] Patterns cleared - channel name is empty.");
            }
        }

        private Regex[] ParseRegexLines(string text, string label)
        {
            var result = new List<Regex>();
            var raw = text ?? string.Empty;
            var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var t = line.Trim();
                if (t.Length == 0)
                    continue;

                try
                {
                    result.Add(new Regex(t, RegexOptions.Compiled | RegexOptions.IgnoreCase));
                }
                catch (ArgumentException ex)
                {
                    AppendLog($"[WARN] Invalid {label} regex \"{t}\": {ex.Message}");
                }
            }

            return result.ToArray();
        }

        private void RefreshQueueDisplay()
        {
            if (Overlay == null)
                return;

            if (Overlay.InvokeRequired)
            {
                Overlay.BeginInvoke(new Action(RefreshQueueDisplay));
                return;
            }

            var items = new List<string>();
            lock (Sync)
            {
                var pos = 1;
                foreach (var name in PlayerQueue)
                {
                    items.Add($"{pos}. {name}");
                    pos++;
                }
            }

            Overlay.UpdateQueue(items);
        }

        private void AppendLog(string message)
        {
            var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";

            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), entry);
                return;
            }

            TxtLog.AppendText(entry + Environment.NewLine);
        }

        private void ApplyOverlayBounds()
        {
            var raw = TxtOverlayBounds.Text;
            if (string.IsNullOrEmpty(raw))
                return;

            var parts = raw.Split(',');
            if (parts.Length != 4)
                return;

            if (int.TryParse(parts[0], out var x)
                && int.TryParse(parts[1], out var y)
                && int.TryParse(parts[2], out var w)
                && int.TryParse(parts[3], out var h))
            {
                Overlay.StartPosition = FormStartPosition.Manual;
                Overlay.Location = new Point(x, y);
                Overlay.Size = new Size(w, h);
            }
        }

        private void ApplyOverlayColors()
        {
            var bg = ParseColor(TxtBgColor.Text, Color.FromArgb(30, 30, 30));
            var fg = ParseColor(TxtFgColor.Text, Color.White);
            BtnBgColor.BackColor = bg;
            BtnFgColor.BackColor = fg;
            Overlay?.SetColors(bg, fg);
        }

        private static Color ParseColor(string text, Color fallback)
        {
            if (string.IsNullOrEmpty(text))
                return fallback;

            var parts = text.Split(',');
            if (parts.Length != 3)
                return fallback;

            if (int.TryParse(parts[0], out var r)
                && int.TryParse(parts[1], out var g)
                && int.TryParse(parts[2], out var b))
                return Color.FromArgb(r, g, b);

            return fallback;
        }

        private void SaveOverlayBounds()
        {
            if (Overlay == null)
                return;

            var b = Overlay.Bounds;
            TxtOverlayBounds.Text = $"{b.X},{b.Y},{b.Width},{b.Height}";
        }

        private void LoadSettings()
        {
            XmlSettings.AddControlSetting(TxtChannelName.Name, TxtChannelName);
            XmlSettings.AddControlSetting(TxtEnqueuePatterns.Name, TxtEnqueuePatterns);
            XmlSettings.AddControlSetting(TxtDequeuePatterns.Name, TxtDequeuePatterns);
            XmlSettings.AddControlSetting(ChkOverlayVisible.Name, ChkOverlayVisible);
            XmlSettings.AddControlSetting(ChkClickThrough.Name, ChkClickThrough);
            XmlSettings.AddControlSetting(TrkOpacity.Name, TrkOpacity);
            XmlSettings.AddControlSetting(TrkFontScale.Name, TrkFontScale);
            XmlSettings.AddControlSetting(TxtOverlayBounds.Name, TxtOverlayBounds);
            XmlSettings.AddControlSetting(TxtBgColor.Name, TxtBgColor);
            XmlSettings.AddControlSetting(TxtFgColor.Name, TxtFgColor);

            if (File.Exists(SettingsFile))
            {
                var fs = new FileStream(SettingsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var xReader = new XmlTextReader(fs);

                try
                {
                    while (xReader.Read())
                        if ((xReader.NodeType == XmlNodeType.Element)
                            && (xReader.LocalName == "SettingsSerializer"))
                            XmlSettings.ImportFromXml(xReader);
                }
                catch (Exception ex)
                {
                    LblStatus.Text = "Error loading settings: " + ex.Message;
                }

                xReader.Close();
            }
        }

        private void SaveSettings()
        {
            var fs = new FileStream(SettingsFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            var xWriter = new XmlTextWriter(fs, Encoding.UTF8)
            {
                Formatting = Formatting.Indented,
                Indentation = 1,
                IndentChar = '\t'
            };
            xWriter.WriteStartDocument(true);
            xWriter.WriteStartElement("Config");
            xWriter.WriteStartElement("SettingsSerializer");
            XmlSettings.ExportToXml(xWriter);
            xWriter.WriteEndElement();
            xWriter.WriteEndElement();
            xWriter.WriteEndDocument();
            xWriter.Flush();
            xWriter.Close();
        }
    }
}