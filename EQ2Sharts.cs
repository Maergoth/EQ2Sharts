using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using Advanced_Combat_Tracker;

namespace EQ2Sharts
{
    public class EQ2ShartsPlugin : UserControl, IActPluginV1
    {
        private readonly LinkedList<string> PlayerQueue = new LinkedList<string>();
        private readonly Dictionary<string, bool> PlayerSet = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> EnqueueTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly object Sync = new object();
        private Timer WaitTimeTimer;

        private Regex ChannelMsgRegex;
        private Regex[] EnqueuePatterns = new Regex[0];
        private Regex[] DequeuePatterns = new Regex[0];

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
        private CheckBox ChkInvertOrder;
        private CheckBox ChkShowWaitTime;
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

        public EQ2ShartsPlugin()
        {
            InitializeComponent();
        }

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

            var lblChannel = new Label { Text = "Channel Names (one per line):", AutoSize = true };
            TxtChannelName = new TextBox
            {
                Name = "txtChannelName",
                Size = new Size(280, 60),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
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

            ChkInvertOrder = new CheckBox
            {
                Name = "chkInvertOrder",
                Text = "Invert (bottom to top)",
                AutoSize = true,
                Checked = false
            };

            ChkShowWaitTime = new CheckBox
            {
                Name = "chkShowWaitTime",
                Text = "Show Wait Time",
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
            rightPanel.Controls.Add(ChkInvertOrder);
            rightPanel.Controls.Add(ChkShowWaitTime);
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

            Name = "EQ2ShartsPlugin";
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
                "Config\\EQ2Sharts.config.xml");

            XmlSettings = new SettingsSerializer(this);
            LoadSettings();

            RebuildPatterns();

            TxtChannelName.TextChanged += delegate { RebuildPatterns(); };
            TxtEnqueuePatterns.TextChanged += delegate { RebuildPatterns(); };
            TxtDequeuePatterns.TextChanged += delegate { RebuildPatterns(); };

            ActGlobals.oFormActMain.OnLogLineRead += OnLogLineRead;

            LblOpacityValue.Text = TrkOpacity.Value.ToString() + "%";
            LblFontScaleValue.Text = TrkFontScale.Value.ToString() + "%";

            Overlay = new QueueOverlayForm();
            ApplyOverlayBounds();
            ApplyOverlayColors();
            Overlay.SetClickThrough(ChkClickThrough.Checked);
            Overlay.Opacity = TrkOpacity.Value / 100.0;
            Overlay.SetFontScale(TrkFontScale.Value / 100f);
            if (ChkOverlayVisible.Checked)
                Overlay.Show();

            ChkOverlayVisible.CheckedChanged += delegate
            {
                if (ChkOverlayVisible.Checked)
                    Overlay.Show();
                else
                    Overlay.Hide();
            };

            ChkClickThrough.CheckedChanged += delegate
            {
                Overlay.SetClickThrough(ChkClickThrough.Checked);
            };

            ChkInvertOrder.CheckedChanged += delegate
            {
                RefreshQueueDisplay();
            };

            ChkShowWaitTime.CheckedChanged += delegate
            {
                if (ChkShowWaitTime.Checked)
                {
                    if (WaitTimeTimer == null)
                    {
                        WaitTimeTimer = new Timer();
                        WaitTimeTimer.Interval = 1000;
                        WaitTimeTimer.Tick += delegate { RefreshQueueDisplay(); };
                    }
                    WaitTimeTimer.Start();
                }
                else
                {
                    if (WaitTimeTimer != null)
                        WaitTimeTimer.Stop();
                }
                RefreshQueueDisplay();
            };

            TrkOpacity.ValueChanged += delegate
            {
                var pct = TrkOpacity.Value;
                LblOpacityValue.Text = pct.ToString() + "%";
                Overlay.Opacity = pct / 100.0;
            };

            TrkFontScale.ValueChanged += delegate
            {
                var pct = TrkFontScale.Value;
                LblFontScaleValue.Text = pct.ToString() + "%";
                Overlay.SetFontScale(pct / 100f);
            };

            BtnBgColor.Click += delegate
            {
                using (var dlg = new ColorDialog { Color = BtnBgColor.BackColor })
                {
                    if (dlg.ShowDialog() != DialogResult.OK)
                        return;

                    BtnBgColor.BackColor = dlg.Color;
                    TxtBgColor.Text = dlg.Color.R.ToString() + "," + dlg.Color.G.ToString() + "," + dlg.Color.B.ToString();
                    ApplyOverlayColors();
                }
            };

            BtnFgColor.Click += delegate
            {
                using (var dlg = new ColorDialog { Color = BtnFgColor.BackColor })
                {
                    if (dlg.ShowDialog() != DialogResult.OK)
                        return;

                    BtnFgColor.BackColor = dlg.Color;
                    TxtFgColor.Text = dlg.Color.R.ToString() + "," + dlg.Color.G.ToString() + "," + dlg.Color.B.ToString();
                    ApplyOverlayColors();
                }
            };

            BtnClearQueue.Click += delegate
            {
                lock (Sync)
                {
                    PlayerQueue.Clear();
                    PlayerSet.Clear();
                    EnqueueTimes.Clear();
                }

                AppendLog("Queue cleared.");
                RefreshQueueDisplay();
            };

            LblStatus.Text = "EQ2Sharts Plugin Started";
        }

        public void DeInitPlugin()
        {
            ActGlobals.oFormActMain.OnLogLineRead -= OnLogLineRead;
            SaveOverlayBounds();
            SaveSettings();

            if (WaitTimeTimer != null)
            {
                WaitTimeTimer.Stop();
                WaitTimeTimer.Dispose();
                WaitTimeTimer = null;
            }

            if (Overlay != null)
            {
                Overlay.Close();
                Overlay.Dispose();
                Overlay = null;
            }

            LblStatus.Text = "EQ2Sharts Plugin Exited";
        }

        private void OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            if (isImport)
                return;

            var line = logInfo.logLine;

            var channelRegex = ChannelMsgRegex;
            if (channelRegex == null)
                return;
            
            // Fast pre-filter: must contain a quote (all chat lines do)
            if (line.IndexOf('"') < 0)
                return;

            var channelMatch = channelRegex.Match(line);
            if (!channelMatch.Success)
                return;

            // Extract player name:
            // Group 1 = name from \aPC link, Group 2 = "You", Group 3 = message content
            var sender = channelMatch.Groups[1].Success ? channelMatch.Groups[1].Value : channelMatch.Groups[2].Value;
            var content = channelMatch.Groups[3].Value;

            AppendLog(string.Format("[DEBUG] Channel message - sender=\"{0}\", content=\"{1}\"", sender, content));

            // Check enqueue patterns - sender gets enqueued
            var enqueuePatterns = EnqueuePatterns;
            foreach (var pattern in enqueuePatterns)
                if (pattern.IsMatch(content))
                {
                    AppendLog(string.Format("[DEBUG] Enqueue pattern matched: /{0}/ - enqueueing \"{1}\"", pattern, sender));
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
                        AppendLog(string.Format("[DEBUG] Dequeue pattern matched: /{0}/ - dequeueing \"{1}\"", pattern, playerName));
                        DequeuePlayer(playerName);
                    }
                    else
                        AppendLog(string.Format("[DEBUG] Dequeue pattern matched: /{0}/ but no capture group found. Add a (capture group) for the player name.", pattern));

                    return;
                }
            }

            AppendLog(string.Format("[DEBUG] Content \"{0}\" matched no enqueue or dequeue pattern.", content));
        }

        private void EnqueuePlayer(string name)
        {
            lock (Sync)
            {
                if (PlayerSet.ContainsKey(name))
                {
                    AppendLog(string.Format("Player \"{0}\" is already in the queue.", name));
                    return;
                }
                PlayerSet.Add(name, true);
                EnqueueTimes[name] = DateTime.Now;

                PlayerQueue.AddLast(name);

                AppendLog(string.Format("Player \"{0}\" added to queue (position {1}).", name, PlayerQueue.Count));
            }

            RefreshQueueDisplay();
        }

        private void DequeuePlayer(string playerName)
        {
            lock (Sync)
            {
                if (!PlayerSet.Remove(playerName))
                {
                    AppendLog(string.Format("Player \"{0}\" is not in the queue.", playerName));
                    return;
                }
                EnqueueTimes.Remove(playerName);

                LinkedListNode<string> node = PlayerQueue.First;
                while (node != null)
                {
                    if (string.Equals(node.Value, playerName, StringComparison.OrdinalIgnoreCase))
                    {
                        PlayerQueue.Remove(node);
                        break;
                    }
                    node = node.Next;
                }

                AppendLog(string.Format("Player \"{0}\" dequeued. Queue size: {1}.", playerName, PlayerQueue.Count));
            }

            RefreshQueueDisplay();
        }

        private void RebuildPatterns()
        {
            EnqueuePatterns = ParseRegexLines(TxtEnqueuePatterns.Text, "Enqueue");
            DequeuePatterns = ParseRegexLines(TxtDequeuePatterns.Text, "Dequeue");

            var channelText = (TxtChannelName.Text ?? string.Empty).Trim();
            if (channelText.Length > 0)
            {
                // Parse multiple channel names (one per line)
                var channelNames = channelText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var escapedChannels = new List<string>();
                foreach (var ch in channelNames)
                {
                    var trimmed = ch.Trim();
                    if (trimmed.Length > 0)
                        escapedChannels.Add(Regex.Escape(trimmed));
                }

                if (escapedChannels.Count == 0)
                {
                    ChannelMsgRegex = null;
                    AppendLog("[DEBUG] Patterns cleared - no valid channel names.");
                    return;
                }

                // Build alternation for channel names: (chan1|chan2|chan3)
                var channelAlt = string.Join("|", escapedChannels.ToArray());

                // Two sender formats:
                //   \aPC -1 Name:Name\/a  (other players)
                //   You                   (yourself)
                // Two verb formats:
                //   tells <channel> (N), "message"
                //   says to the <channel> party, "message"
                // Combined with alternation for both sender formats:
                var verbPart = "(?:tells? (?:" + channelAlt + ")(?: \\(\\d+\\))?,|says? to the (?:" + channelAlt + ")[^,]*,) \"(.+?)\"";
                var channelPattern = "(?:\\\\aPC -?\\d+ [^:]+:(\\w+)\\\\/a|(You)) " + verbPart;

                ChannelMsgRegex = new Regex(channelPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

                AppendLog(string.Format("[DEBUG] Patterns rebuilt - channels=\"{0}\"", channelText.Replace("\r", "").Replace("\n", ", ")));
                AppendLog(string.Format("[DEBUG]   Channel regex: {0}", channelPattern));
                AppendLog(string.Format("[DEBUG]   Enqueue patterns: {0}", EnqueuePatterns.Length));
                AppendLog(string.Format("[DEBUG]   Dequeue patterns: {0}", DequeuePatterns.Length));
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
                    AppendLog(string.Format("[WARN] Invalid {0} regex \"{1}\": {2}", label, t, ex.Message));
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
            bool showWait = ChkShowWaitTime.Checked;
            var now = DateTime.Now;
            lock (Sync)
            {
                var pos = 1;
                foreach (var name in PlayerQueue)
                {
                    var entry = pos.ToString() + ". " + name;
                    if (showWait)
                    {
                        DateTime enqTime;
                        if (EnqueueTimes.TryGetValue(name, out enqTime))
                        {
                            var elapsed = now - enqTime;
                            if (elapsed.TotalHours >= 1)
                                entry += " (" + ((int)elapsed.TotalHours).ToString() + "h " + elapsed.Minutes.ToString() + "m)";
                            else if (elapsed.TotalMinutes >= 1)
                                entry += " (" + elapsed.Minutes.ToString() + "m " + elapsed.Seconds.ToString() + "s)";
                            else
                                entry += " (" + elapsed.Seconds.ToString() + "s)";
                        }
                    }
                    items.Add(entry);
                    pos++;
                }
            }

            // If invert is checked, reverse the list order (bottom to top)
            if (ChkInvertOrder.Checked)
                items.Reverse();

            Overlay.UpdateQueue(items);
        }

        private void AppendLog(string message)
        {
            var entry = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;

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

            int x, y, w, h;
            if (int.TryParse(parts[0], out x)
                && int.TryParse(parts[1], out y)
                && int.TryParse(parts[2], out w)
                && int.TryParse(parts[3], out h))
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
            if (Overlay != null)
                Overlay.SetColors(bg, fg);
        }

        private static Color ParseColor(string text, Color fallback)
        {
            if (string.IsNullOrEmpty(text))
                return fallback;

            var parts = text.Split(',');
            if (parts.Length != 3)
                return fallback;

            int r, g, b;
            if (int.TryParse(parts[0], out r)
                && int.TryParse(parts[1], out g)
                && int.TryParse(parts[2], out b))
                return Color.FromArgb(r, g, b);

            return fallback;
        }

        private void SaveOverlayBounds()
        {
            if (Overlay == null)
                return;

            var bounds = Overlay.Bounds;
            TxtOverlayBounds.Text = bounds.X.ToString() + "," + bounds.Y.ToString() + "," + bounds.Width.ToString() + "," + bounds.Height.ToString();
        }

        private void LoadSettings()
        {
            XmlSettings.AddControlSetting(TxtChannelName.Name, TxtChannelName);
            XmlSettings.AddControlSetting(TxtEnqueuePatterns.Name, TxtEnqueuePatterns);
            XmlSettings.AddControlSetting(TxtDequeuePatterns.Name, TxtDequeuePatterns);
            XmlSettings.AddControlSetting(ChkOverlayVisible.Name, ChkOverlayVisible);
            XmlSettings.AddControlSetting(ChkClickThrough.Name, ChkClickThrough);
            XmlSettings.AddControlSetting(ChkInvertOrder.Name, ChkInvertOrder);
            XmlSettings.AddControlSetting(ChkShowWaitTime.Name, ChkShowWaitTime);
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

    /// <summary>
    /// Overlay form that displays the queue on top of the game.
    /// </summary>
    public sealed class QueueOverlayForm : Form
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;

        private readonly Label LblQueue;
        private bool ClickThrough;
        private const float BaseFontSize = 10f;

        public QueueOverlayForm()
        {
            Text = "EQ2Sharts Queue";
            Size = new Size(200, 300);
            MinimumSize = new Size(120, 100);
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - 220, 100);
            BackColor = Color.FromArgb(30, 30, 30);

            LblQueue = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", BaseFontSize),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                AutoSize = false,
                Padding = new Padding(4)
            };

            Controls.Add(LblQueue);
        }

        public void SetClickThrough(bool enabled)
        {
            if (ClickThrough == enabled)
                return;

            ClickThrough = enabled;

            if (enabled)
            {
                FormBorderStyle = FormBorderStyle.None;

                if (IsHandleCreated)
                {
                    var exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
                    SetWindowLong(Handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
                }
            }
            else
            {
                if (IsHandleCreated)
                {
                    var exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
                    SetWindowLong(Handle, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
                }

                FormBorderStyle = FormBorderStyle.SizableToolWindow;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;

                if (ClickThrough)
                    cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED;

                return cp;
            }
        }

        public void SetFontScale(float scale)
        {
            var newSize = Math.Max(BaseFontSize * scale, 4f);
            LblQueue.Font = new Font(LblQueue.Font.FontFamily, newSize, LblQueue.Font.Style);
        }

        public void SetColors(Color background, Color foreground)
        {
            BackColor = background;
            LblQueue.BackColor = background;
            LblQueue.ForeColor = foreground;
        }

        public void UpdateQueue(List<string> items)
        {
            LblQueue.Text = string.Join(Environment.NewLine, items);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }

            base.OnFormClosing(e);
        }
    }
}
