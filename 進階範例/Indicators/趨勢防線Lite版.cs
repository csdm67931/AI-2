﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using Runtime.Script;
using Runtime.Script.Charts;
using TradeApi;
using TradeApi.Trading;
using TradeApi.History;
using TradeApi.Indicators;
using TradeApi.Instruments;
using TradeApi.ToolBelt;

namespace TrendGuardLite
{
    /// <summary>
    /// 趨勢防線 Lite 版
    /// 功能：偵測價格是否觸碰使用者設定的防守線（水平線/趨勢線）
    /// </summary>
    public class TrendGuardLite : IndicatorBuilder
    {
        // ════════════════════════════════════════════════════════════════
        // I. 權限設定 
        // ════════════════════════════════════════════════════════════════
        private readonly List<string> AllowedAccounts = new List<string>
        {
            "63160", "63093", "4623377", "4624033", "6862079", "5729207",
            "4626439", "5712450", "4626633", "6858148", "9078873", "4627302",
            "4628369", "4628372", "4627849", "4628466", "82027", "9066966",
            "81435", "80938", "4622831", "63166", "81806", "4628709",
            "4602916", "81908", "4622323", "0922234", "2566850", "4629180", "81974",
        };

        // ════════════════════════════════════════════════════════════════
        // II. 參數設定
        // ════════════════════════════════════════════════════════════════
        
        [InputParameter(InputType.Checkbox, "啟用聲音警報", 10)]
        public bool enableSoundAlert = false;
        
        [InputParameter(InputType.Checkbox, "啟用彈窗通知", 20)]
        public bool enablePopupAlert = false;
        
        [InputParameter(InputType.Checkbox, "📱 啟用 Telegram 通知", 30)]
        public bool enableTelegram = false;
        
        [InputParameter(InputType.String, "Telegram Bot Token", 40)]
        public string telegramBotToken = "";
        
        [InputParameter(InputType.String, "Telegram Chat ID", 50)]
        public string telegramChatId = "";
        
        // ════════════════════════════════════════════════════════════════
        // III. 防守線資料結構
        // ════════════════════════════════════════════════════════════════
        
        public enum GuardLineType { Horizontal = 1, TrendLine = 2 }
        
        public class GuardLine
        {
            public int Id { get; set; }
            public GuardLineType Type { get; set; }
            public double Price1 { get; set; }
            public double Price2 { get; set; }
            public DateTime Time1 { get; set; }
            public DateTime Time2 { get; set; }
            public Color LineColor { get; set; }
            public bool IsTriggered { get; set; }
            public bool? LastPriceAboveLine { get; set; }
            public bool IsInitialized { get; set; }
            
            public double GetPriceAtTime(DateTime time)
            {
                if (Type == GuardLineType.Horizontal) return Price1;
                if (Time2 == Time1) return Price1;
                double timeFraction = (time - Time1).TotalSeconds / (Time2 - Time1).TotalSeconds;
                return Price1 + (Price2 - Price1) * timeFraction;
            }
        }
        
        private List<GuardLine> guardLines = new List<GuardLine>();
        private int nextLineId = 1;
        private HashSet<int> triggeredLineIds = new HashSet<int>();
        
        // ════════════════════════════════════════════════════════════════
        // IV. UI 變數
        // ════════════════════════════════════════════════════════════════
        
        Control chrt;
        private string symb = "";
        private Account currentAccount;
        
        private Button btn_Toggle;
        private Button btn_DrawHoriz;
        private Button btn_DrawTrend;
        private Button btn_ClearAll;
        private Label lbl_Status;
        
        private bool isMonitoring = false;
        
        private enum DrawingMode { None, Horizontal, TrendLine }
        private DrawingMode currentDrawingMode = DrawingMode.None;
        private bool isDrawingTrendLine = false;
        private DateTime trendLineStartTime;
        private double trendLineStartPrice;
        private Point currentMousePosition;
        private bool isMouseOnChart = false;
        
        private bool isDraggingLine = false;
        private GuardLine draggingLine = null;
        private double dragStartPrice;
        
        private double currentClose, currentHigh, currentLow;
        private DateTime lastAlertTime = DateTime.MinValue;
        
        // ════════════════════════════════════════════════════════════════
        // V. 初始化
        // ════════════════════════════════════════════════════════════════
        
        public TrendGuardLite() : base()
        {
            #region Initialization
            Credentials.Author = "CapitalFutures Corp.";
            Credentials.Company = "CapitalFutures Corp.";
            Credentials.Copyrights = "CapitalFutures Corp.";
            Credentials.DateOfCreation = new DateTime(2026, 1, 16);
            Credentials.ExpirationDate = new DateTime(2026, 6, 1);
            Credentials.Version = "1.0";
            Credentials.Password = "66b4a6416f59370e942d353f08a9ae36";
            Credentials.ProjectName = "趨勢防線Lite";
            #endregion
            
            SeparateWindow = false;
        }

        public override void Init()
        {
            currentAccount = AccountManager.Current;
            if (currentAccount == null || !AllowedAccounts.Contains(currentAccount.Name)) return;

            symb = InstrumentsManager.Current.Symbol;
            chrt = this.ChartSource.GetChartControl() as Control;
            if (chrt == null) return;
            
            try
            {
                Action initAction = () =>
                {
                    chrt.MouseClick += Chart_MouseClick;
                    chrt.MouseMove += Chart_MouseMove;
                    chrt.MouseLeave += Chart_MouseLeave;
                    chrt.MouseDown += Chart_MouseDown;
                    chrt.MouseUp += Chart_MouseUp;
                    CreateControls();
                    UpdateStatusDisplay();
                };

                if (chrt.InvokeRequired)
                    chrt.BeginInvoke(initAction);
                else
                    initAction();
            }
            catch { }
        }

        public override void Update(TickStatus args)
        {
            if (currentAccount == null || !AllowedAccounts.Contains(currentAccount.Name)) return;
            
            currentClose = HistoryDataSeries.GetValue(PriceType.Close, 0);
            currentHigh = HistoryDataSeries.GetValue(PriceType.High, 0);
            currentLow = HistoryDataSeries.GetValue(PriceType.Low, 0);
            
            if (isMonitoring && guardLines.Count > 0)
                CheckGuardLineTrigger();
        }

        public override void OnPaintChart(object sender, PaintChartEventArgs args)
        {
            if (currentAccount == null || !AllowedAccounts.Contains(currentAccount.Name))
            {
                using (var font = new Font("Arial", 17, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.Red))
                {
                    args.Graphics.DrawString("此指標僅限授權帳號使用，請聯絡營業員", font, brush, 5, 60);
                }
                return;
            }
            
            DrawHorizontalLines(args);
            DrawTrendLines(args);
        }

        public override void Complete()
        {
            DisposeAllControls();
            if (chrt != null)
            {
                chrt.MouseClick -= Chart_MouseClick;
                chrt.MouseMove -= Chart_MouseMove;
                chrt.MouseLeave -= Chart_MouseLeave;
                chrt.MouseDown -= Chart_MouseDown;
                chrt.MouseUp -= Chart_MouseUp;
            }
        }
        
        // ════════════════════════════════════════════════════════════════
        // VI. 防守線觸發檢測
        // ════════════════════════════════════════════════════════════════
        
        private void CheckGuardLineTrigger()
        {
            DateTime currentTime = HistoryDataSeries.GetTimeUtc(0);
            
            foreach (var line in guardLines.Where(l => !l.IsTriggered))
            {
                double guardPrice = line.GetPriceAtTime(currentTime);
                
                if (!line.IsInitialized)
                {
                    line.LastPriceAboveLine = currentClose > guardPrice;
                    line.IsInitialized = true;
                    continue;
                }
                
                bool crossDown = line.LastPriceAboveLine == true && currentLow < guardPrice;
                bool crossUp = line.LastPriceAboveLine == false && currentHigh > guardPrice;
                
                line.LastPriceAboveLine = currentClose > guardPrice;
                
                if (crossDown || crossUp)
                    OnGuardLineTriggered(line, guardPrice, crossDown ? "向下穿越" : "向上穿越");
            }
        }
        
        private void OnGuardLineTriggered(GuardLine line, double triggerPrice, string direction)
        {
            if ((DateTime.Now - lastAlertTime).TotalSeconds < 3) return;
            lastAlertTime = DateTime.Now;
            
            line.IsTriggered = true;
            triggeredLineIds.Add(line.Id);
            
            string alertMsg = $"【{symb}】{direction}\n防守線{line.Id} @ {triggerPrice:F2}";
            Notification.Alert(alertMsg);
            
            if (enableTelegram)
                SendTelegramNotify($"🔔 {alertMsg}");
            
            if (enableSoundAlert)
                System.Media.SystemSounds.Exclamation.Play();
            
            if (enablePopupAlert)
            {
                MessageBox.Show(alertMsg, $"⚠️ 【{symb}】趨勢防線觸發", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            }
            
            UpdateStatusDisplay();
        }
        
        private void SendTelegramNotify(string message)
        {
            if (!enableTelegram || string.IsNullOrEmpty(telegramBotToken) || string.IsNullOrEmpty(telegramChatId))
                return;
            
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using (var client = new System.Net.WebClient())
                    {
                        string url = $"https://api.telegram.org/bot{telegramBotToken}/sendMessage";
                        client.Headers[System.Net.HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                        string postData = $"chat_id={telegramChatId}&text={Uri.EscapeDataString(message)}";
                        client.UploadString(url, postData);
                    }
                }
                catch { }
            });
        }
        
        // ════════════════════════════════════════════════════════════════
        // VII. 防守線管理
        // ════════════════════════════════════════════════════════════════
        
        private void AddHorizontalLine(double price)
        {
            var newLine = new GuardLine
            {
                Id = nextLineId++,
                Type = GuardLineType.Horizontal,
                Price1 = price,
                LineColor = Color.Red,
                IsTriggered = false
            };
            guardLines.Add(newLine);
            UpdateStatusDisplay();
            
            if (enableSoundAlert)
                System.Media.SystemSounds.Asterisk.Play();
        }
        
        private void AddTrendLine(double price1, DateTime time1, double price2, DateTime time2)
        {
            var newLine = new GuardLine
            {
                Id = nextLineId++,
                Type = GuardLineType.TrendLine,
                Price1 = price1,
                Price2 = price2,
                Time1 = time1,
                Time2 = time2,
                LineColor = Color.Orange,
                IsTriggered = false
            };
            guardLines.Add(newLine);
            UpdateStatusDisplay();
            
            if (enableSoundAlert)
                System.Media.SystemSounds.Asterisk.Play();
        }
        
        private void ClearAllLines()
        {
            guardLines.Clear();
            triggeredLineIds.Clear();
            nextLineId = 1;
            isDrawingTrendLine = false;
            currentDrawingMode = DrawingMode.None;
            
            if (btn_DrawTrend != null) { btn_DrawTrend.BackColor = Color.FromArgb(80, 80, 90); btn_DrawTrend.Text = "畫趨勢線"; }
            if (btn_DrawHoriz != null) { btn_DrawHoriz.BackColor = Color.FromArgb(80, 80, 90); btn_DrawHoriz.Text = "畫水平線"; }
            
            UpdateStatusDisplay();
        }
        
        private void DeleteNearestLine(double clickPrice, DateTime clickTime)
        {
            if (guardLines.Count == 0) return;
            
            GuardLine nearestLine = null;
            double minDistance = double.MaxValue;
            
            foreach (var line in guardLines)
            {
                double linePrice = line.GetPriceAtTime(clickTime);
                double distance = Math.Abs(clickPrice - linePrice);
                if (distance < minDistance) { minDistance = distance; nearestLine = line; }
            }
            
            if (nearestLine != null)
            {
                guardLines.Remove(nearestLine);
                triggeredLineIds.Remove(nearestLine.Id);
                UpdateStatusDisplay();
                if (enableSoundAlert) System.Media.SystemSounds.Exclamation.Play();
            }
        }
        
        // ════════════════════════════════════════════════════════════════
        // VIII. UI 繪製
        // ════════════════════════════════════════════════════════════════
        
        private void DrawHorizontalLines(PaintChartEventArgs args)
        {
            var mainWindow = ChartSource.GetAllWindows().FirstOrDefault(w => w.IsMainWindow);
            if (mainWindow == null) return;
            
            float chartWidth = (float)args.Graphics.VisibleClipBounds.Width;
            
            foreach (var line in guardLines.Where(l => l.Type == GuardLineType.Horizontal))
            {
                try
                {
                    PointF point = ChartSource.GetChartPoint(
                        new TimeValue(DateTime.Now, line.Price1), mainWindow.WindowNumber);
                    
                    Color lineColor = line.IsTriggered ? Color.Gray : line.LineColor;
                    
                    using (var pen = new Pen(lineColor, 2))
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                        args.Graphics.DrawLine(pen, 0, point.Y, chartWidth, point.Y);
                    }
                    
                    float labelX = chartWidth - 200;
                    using (var font = new Font("Arial", 9, FontStyle.Bold))
                    using (var brush = new SolidBrush(lineColor))
                    using (var bgBrush = new SolidBrush(Color.FromArgb(180, 30, 30, 35)))
                    {
                        string statusIcon = line.IsTriggered ? "✓" : "●";
                        string label = $"防守線{line.Id}: {line.Price1:F2} {statusIcon}";
                        SizeF textSize = args.Graphics.MeasureString(label, font);
                        args.Graphics.FillRectangle(bgBrush, labelX - 3, point.Y - 10, textSize.Width + 6, textSize.Height + 2);
                        args.Graphics.DrawString(label, font, brush, labelX, point.Y - 8);
                    }
                }
                catch { }
            }
        }
        
        private void DrawTrendLines(PaintChartEventArgs args)
        {
            var mainWindow = ChartSource.GetAllWindows().FirstOrDefault(w => w.IsMainWindow);
            if (mainWindow == null) return;
            
            foreach (var line in guardLines.Where(l => l.Type == GuardLineType.TrendLine))
            {
                try
                {
                    PointF point1 = ChartSource.GetChartPoint(new TimeValue(line.Time1, line.Price1), mainWindow.WindowNumber);
                    PointF point2 = ChartSource.GetChartPoint(new TimeValue(line.Time2, line.Price2), mainWindow.WindowNumber);
                    
                    Color lineColor = line.IsTriggered ? Color.Gray : line.LineColor;
                    using (var pen = new Pen(lineColor, 2))
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        args.Graphics.DrawLine(pen, point1, point2);
                        
                        if (!line.IsTriggered)
                        {
                            float dx = point2.X - point1.X;
                            float dy = point2.Y - point1.Y;
                            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                            args.Graphics.DrawLine(pen, point2, new PointF(point2.X + dx * 5, point2.Y + dy * 5));
                        }
                    }
                    
                    using (var font = new Font("Arial", 9, FontStyle.Bold))
                    using (var brush = new SolidBrush(lineColor))
                    {
                        string statusIcon = line.IsTriggered ? "✓" : "●";
                        args.Graphics.DrawString($"防守線{line.Id}: {line.Price1:F2} {statusIcon}", font, brush, point1.X + 5, point1.Y - 15);
                    }
                }
                catch { }
            }
            
            // 趨勢線預覽
            if (isDrawingTrendLine && isMouseOnChart)
            {
                try
                {
                    PointF startPoint = ChartSource.GetChartPoint(
                        new TimeValue(trendLineStartTime, trendLineStartPrice), mainWindow.WindowNumber);
                    Point chartMousePos = ChartSource.PointToChart(chrt.PointToScreen(currentMousePosition));
                    TimeValue mouseTimeValue = ChartSource.GetTimeValue(chartMousePos, mainWindow.WindowNumber);
                    PointF endPoint = ChartSource.GetChartPoint(mouseTimeValue, mainWindow.WindowNumber);
                    
                    using (var pen = new Pen(Color.Orange, 2))
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        args.Graphics.DrawLine(pen, startPoint, endPoint);
                    }
                    using (var brush = new SolidBrush(Color.Orange))
                    {
                        args.Graphics.FillEllipse(brush, startPoint.X - 5, startPoint.Y - 5, 10, 10);
                    }
                    using (var pen = new Pen(Color.Orange, 2))
                    {
                        args.Graphics.DrawEllipse(pen, endPoint.X - 5, endPoint.Y - 5, 10, 10);
                    }
                }
                catch { }
            }
        }
        
        // ════════════════════════════════════════════════════════════════
        // IX. UI 控制項與事件處理
        // ════════════════════════════════════════════════════════════════
        
        private void CreateControls()
        {
            Color forecolor = Color.White;
            int startX = (int)chrt.Width - 280;
            int startY = 35;
            int buttonHeight = 26;
            int buttonWidth = 70;
            int gap = 5;
            
            // 按鈕1: 運作開關
            btn_Toggle = new Button
            {
                Text = "已暫停", Tag = symb,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Location = new Point(startX, startY),
                Size = new Size(buttonWidth, buttonHeight),
                BackColor = Color.FromArgb(180, 60, 60),
                ForeColor = forecolor,
                FlatStyle = FlatStyle.Flat,
                Anchor = (AnchorStyles.Top | AnchorStyles.Right),
            };
            btn_Toggle.FlatAppearance.BorderSize = 1;
            btn_Toggle.Click += Btn_Toggle_Click;
            chrt.Controls.Add(btn_Toggle);
            
            // 按鈕2: 畫水平線
            btn_DrawHoriz = new Button
            {
                Text = "畫水平線", Tag = symb,
                Font = new Font("Arial", 9, FontStyle.Bold),
                Location = new Point(startX + buttonWidth + gap, startY),
                Size = new Size(buttonWidth, buttonHeight),
                BackColor = Color.FromArgb(80, 80, 90),
                ForeColor = forecolor,
                FlatStyle = FlatStyle.Flat,
                Anchor = (AnchorStyles.Top | AnchorStyles.Right),
            };
            btn_DrawHoriz.FlatAppearance.BorderSize = 1;
            btn_DrawHoriz.Click += Btn_DrawHoriz_Click;
            chrt.Controls.Add(btn_DrawHoriz);
            
            // 按鈕3: 畫趨勢線
            btn_DrawTrend = new Button
            {
                Text = "畫趨勢線", Tag = symb,
                Font = new Font("Arial", 9, FontStyle.Bold),
                Location = new Point(startX + (buttonWidth + gap) * 2, startY),
                Size = new Size(buttonWidth, buttonHeight),
                BackColor = Color.FromArgb(80, 80, 90),
                ForeColor = forecolor,
                FlatStyle = FlatStyle.Flat,
                Anchor = (AnchorStyles.Top | AnchorStyles.Right),
            };
            btn_DrawTrend.FlatAppearance.BorderSize = 1;
            btn_DrawTrend.Click += Btn_DrawTrend_Click;
            chrt.Controls.Add(btn_DrawTrend);
            
            // 按鈕4: 清除畫線
            btn_ClearAll = new Button
            {
                Text = "清除畫線", Tag = symb,
                Font = new Font("Arial", 9, FontStyle.Bold),
                Location = new Point(startX + (buttonWidth + gap) * 3, startY),
                Size = new Size(buttonWidth, buttonHeight),
                BackColor = Color.FromArgb(80, 80, 90),
                ForeColor = forecolor,
                FlatStyle = FlatStyle.Flat,
                Anchor = (AnchorStyles.Top | AnchorStyles.Right),
            };
            btn_ClearAll.FlatAppearance.BorderSize = 1;
            btn_ClearAll.Click += (s, e) => ClearAllLines();
            chrt.Controls.Add(btn_ClearAll);
            
            // 狀態列
            lbl_Status = new Label
            {
                Text = "💡 點選繪圖按鈕開始畫線 | 右鍵刪除", Tag = symb,
                Font = new Font("Arial", 9),
                Location = new Point(startX, startY + buttonHeight + gap),
                Size = new Size(290, 18),
                ForeColor = Color.LightGray,
                BackColor = Color.Transparent,
                Anchor = (AnchorStyles.Top | AnchorStyles.Right),
            };
            chrt.Controls.Add(lbl_Status);
        }
        
        private void Btn_Toggle_Click(object sender, EventArgs e)
        {
            isMonitoring = !isMonitoring;
            
            if (isMonitoring)
            {
                btn_Toggle.Text = "運作中";
                btn_Toggle.BackColor = Color.FromArgb(60, 160, 60);
                triggeredLineIds.Clear();
                foreach (var line in guardLines)
                {
                    line.IsTriggered = false;
                    line.IsInitialized = false;
                    line.LastPriceAboveLine = null;
                }
            }
            else
            {
                btn_Toggle.Text = "已暫停";
                btn_Toggle.BackColor = Color.FromArgb(180, 60, 60);
            }
            UpdateStatusDisplay();
        }
        
        private void Btn_DrawHoriz_Click(object sender, EventArgs e)
        {
            if (currentDrawingMode == DrawingMode.Horizontal)
            {
                currentDrawingMode = DrawingMode.None;
                btn_DrawHoriz.BackColor = Color.FromArgb(80, 80, 90);
                btn_DrawHoriz.Text = "畫水平線";
            }
            else
            {
                currentDrawingMode = DrawingMode.Horizontal;
                isDrawingTrendLine = false;
                btn_DrawHoriz.BackColor = Color.FromArgb(0, 120, 200);
                btn_DrawHoriz.Text = "繪製中";
                btn_DrawTrend.BackColor = Color.FromArgb(80, 80, 90);
                btn_DrawTrend.Text = "畫趨勢線";
            }
            UpdateStatusDisplay();
        }
        
        private void Btn_DrawTrend_Click(object sender, EventArgs e)
        {
            if (currentDrawingMode == DrawingMode.TrendLine)
            {
                currentDrawingMode = DrawingMode.None;
                isDrawingTrendLine = false;
                btn_DrawTrend.BackColor = Color.FromArgb(80, 80, 90);
                btn_DrawTrend.Text = "畫趨勢線";
            }
            else
            {
                currentDrawingMode = DrawingMode.TrendLine;
                isDrawingTrendLine = false;
                btn_DrawTrend.BackColor = Color.FromArgb(200, 120, 0);
                btn_DrawTrend.Text = "繪製中";
                btn_DrawHoriz.BackColor = Color.FromArgb(80, 80, 90);
                btn_DrawHoriz.Text = "畫水平線";
            }
            UpdateStatusDisplay();
        }
        
        private void Chart_MouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                Point screenPoint = chrt.PointToScreen(e.Location);
                Point chartPosition = ChartSource.PointToChart(screenPoint);
                TimeValue timeValue = ChartSource.GetTimeValue(chartPosition, 0);
                double price = Math.Round(timeValue.Value, GetDecimalPlace());
                DateTime clickTime = timeValue.TimeUtc;
                
                if (price <= 0) return;
                
                if (e.Button == MouseButtons.Right)
                {
                    DeleteNearestLine(price, clickTime);
                    return;
                }
                
                if (e.Button != MouseButtons.Left) return;
                
                if (currentDrawingMode == DrawingMode.Horizontal)
                {
                    AddHorizontalLine(price);
                }
                else if (currentDrawingMode == DrawingMode.TrendLine)
                {
                    if (!isDrawingTrendLine)
                    {
                        trendLineStartTime = clickTime;
                        trendLineStartPrice = price;
                        isDrawingTrendLine = true;
                        UpdateStatusDisplay();
                    }
                    else
                    {
                        AddTrendLine(trendLineStartPrice, trendLineStartTime, price, clickTime);
                        isDrawingTrendLine = false;
                        currentDrawingMode = DrawingMode.None;
                        btn_DrawTrend.BackColor = Color.FromArgb(80, 80, 90);
                        btn_DrawTrend.Text = "畫趨勢線";
                        UpdateStatusDisplay();
                    }
                }
            }
            catch { }
        }
        
        private void Chart_MouseMove(object sender, MouseEventArgs e)
        {
            currentMousePosition = e.Location;
            isMouseOnChart = true;
            
            if (isDraggingLine && draggingLine != null)
            {
                try
                {
                    var mainWindow = ChartSource.GetAllWindows().FirstOrDefault(w => w.IsMainWindow);
                    if (mainWindow != null)
                    {
                        Point chartMousePos = ChartSource.PointToChart(chrt.PointToScreen(e.Location));
                        TimeValue mouseTimeValue = ChartSource.GetTimeValue(chartMousePos, mainWindow.WindowNumber);
                        
                        if (draggingLine.Type == GuardLineType.Horizontal)
                            draggingLine.Price1 = mouseTimeValue.Value;
                        else if (draggingLine.Type == GuardLineType.TrendLine)
                        {
                            double priceChange = mouseTimeValue.Value - dragStartPrice;
                            draggingLine.Price1 += priceChange;
                            draggingLine.Price2 += priceChange;
                        }
                        dragStartPrice = mouseTimeValue.Value;
                        ChartSource.GUIRefresh();
                    }
                }
                catch { }
            }
            else if (isDrawingTrendLine)
            {
                ChartSource.GUIRefresh();
            }
        }
        
        private void Chart_MouseLeave(object sender, EventArgs e)
        {
            isMouseOnChart = false;
            if (isDraggingLine) { isDraggingLine = false; draggingLine = null; }
        }
        
        private void Chart_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || currentDrawingMode != DrawingMode.None) return;
            
            try
            {
                var mainWindow = ChartSource.GetAllWindows().FirstOrDefault(w => w.IsMainWindow);
                if (mainWindow == null) return;
                
                Point chartMousePos = ChartSource.PointToChart(chrt.PointToScreen(e.Location));
                TimeValue mouseTimeValue = ChartSource.GetTimeValue(chartMousePos, mainWindow.WindowNumber);
                double clickPrice = mouseTimeValue.Value;
                
                foreach (var line in guardLines.Where(l => !l.IsTriggered))
                {
                    double linePrice = line.Type == GuardLineType.Horizontal ? line.Price1 : line.GetPriceAtTime(DateTime.Now);
                    PointF linePoint = ChartSource.GetChartPoint(new TimeValue(DateTime.Now, linePrice), mainWindow.WindowNumber);
                    PointF clickPoint = ChartSource.GetChartPoint(mouseTimeValue, mainWindow.WindowNumber);
                    double pixelDistance = Math.Abs(linePoint.Y - clickPoint.Y);
                    
                    if (pixelDistance < 10)
                    {
                        isDraggingLine = true;
                        draggingLine = line;
                        dragStartPrice = clickPrice;
                        break;
                    }
                }
            }
            catch { }
        }
        
        private void Chart_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDraggingLine && draggingLine != null)
            {
                draggingLine.IsInitialized = false;
                draggingLine.LastPriceAboveLine = null;
                isDraggingLine = false;
                draggingLine = null;
            }
        }
        
        private void UpdateStatusDisplay()
        {
            if (lbl_Status == null) return;
            
            try
            {
                if (chrt.InvokeRequired) { chrt.BeginInvoke(new Action(UpdateStatusDisplay)); return; }
                
                int activeCount = guardLines.Count(l => !l.IsTriggered);
                int triggeredCount = triggeredLineIds.Count;
                
                if (currentDrawingMode == DrawingMode.TrendLine)
                {
                    lbl_Status.Text = isDrawingTrendLine ? 
                        $"📐 已設起點 {trendLineStartPrice:F2} | 請點擊第二點" : 
                        "📐 趨勢線模式 | 請點擊第一點";
                    lbl_Status.ForeColor = Color.Orange;
                }
                else if (currentDrawingMode == DrawingMode.Horizontal)
                {
                    lbl_Status.Text = "📏 水平線模式 | 請點擊設定價格";
                    lbl_Status.ForeColor = Color.FromArgb(100, 180, 255);
                }
                else if (guardLines.Count == 0)
                {
                    lbl_Status.Text = "💡 點選繪圖按鈕開始畫線 | 右鍵刪除";
                    lbl_Status.ForeColor = Color.LightGray;
                }
                else if (!isMonitoring)
                {
                    lbl_Status.Text = $"已設 {guardLines.Count} 條線 | 按開關啟動 | 右鍵刪除";
                    lbl_Status.ForeColor = Color.Orange;
                }
                else if (triggeredCount == guardLines.Count)
                {
                    lbl_Status.Text = $"✅ 所有防守線已觸發 ({triggeredCount}/{guardLines.Count})";
                    lbl_Status.ForeColor = Color.Gray;
                }
                else
                {
                    lbl_Status.Text = $"🔍 監控中: {activeCount} 條 | 已觸發: {triggeredCount}";
                    lbl_Status.ForeColor = Color.Lime;
                }
            }
            catch { }
        }
        
        private void DisposeAllControls()
        {
            try
            {
                if (chrt == null) return;
                foreach (Control ctrl in chrt.Controls.Cast<Control>().ToArray())
                {
                    if (ctrl.Tag != null && (string)ctrl.Tag == symb)
                    {
                        chrt.Controls.Remove(ctrl);
                        ctrl.Dispose();
                    }
                }
            }
            catch { }
        }
        
        // ════════════════════════════════════════════════════════════════
        // X. 輔助方法
        // ════════════════════════════════════════════════════════════════
        
        private int GetDecimalPlace()
        {
            double miniticksize = InstrumentsManager.Current.MinimalTickSize;
            string tickString = miniticksize.ToString("F20");
            int dotIndex = tickString.IndexOf('.');
            if (dotIndex < 0) return 0;
            string decimalPart = tickString.Substring(dotIndex + 1).TrimEnd('0');
            return Math.Min(decimalPart.Length, 15);
        }
    }
}
