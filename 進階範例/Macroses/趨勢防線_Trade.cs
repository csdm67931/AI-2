using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
using System.Globalization;
using System.IO;
using System.Timers;

namespace TrendGuardTrade
{
    /// <summary>
    /// 趨勢防線_交易版
    /// 功能：接收「趨勢防線」指標的訊號，執行按比例平倉
    /// </summary>
    public class TrendGuardTrade : StrategyBuilder
    {
        // ════════════════════════════════════════════════════════════════
        // I. 權限設定 
        // ════════════════════════════════════════════════════════════════
        private readonly List<string> AllowedAccounts = new List<string>
        {
                        "63160", //變更帳號
            "63093",
            "4623377",
            "4624033",
            "6862079",
            "5729207",
            "4626439",
            "5712450",
            "4626633",  //王家奉
            "6858148",
            "9078873",
            "4627302",
            "4628369", //劉子瑄
            "4628372", //陳盈年
            "4627849",
            "4628466", //雷竼萱
            "82027",   //雷竼萱模擬帳號
            "9066966",
            "81435",
            "80938",   //瑞珍模擬帳號
            "4622831", //瑞珍真倉
            "63166",   //DREAM老師
            "81806",   //周明恩模擬帳號
            "4628709", //周明恩
            "4602916", //何智偉
            "81908",   //陳語婕模擬帳號
            "4622323",
            "0922234", //黃茂銓
            "2566850", //黃育泉
            "4629180", //何宗伯
			"81974",   //何宗伯模擬帳號
        };

        // ════════════════════════════════════════════════════════════════
        // II. 參數設定
        // ════════════════════════════════════════════════════════════════
        
        [InputParameter(InputType.Numeric, "策略幻數 (Magic Number)", 0)]
        public int MagicNumber { get; set; } = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
        
        [InputParameter(InputType.Checkbox, "需要確認才執行", 10)]
        public bool requireConfirmation = false;  // 預設直接執行，不彈確認框
        
        [InputParameter(InputType.Checkbox, "啟用訊息記錄", 20)]
        public bool enableLogMessage = true;
        
        // ════════════════════════════════════════════════════════════════
        // III. 變數
        // ════════════════════════════════════════════════════════════════
        
        private Account currentAccount;
        private long lastProcessedSignalTime = 0;
        private System.Timers.Timer signalCheckTimer;
        private bool isProcessingSignal = false;

        // ════════════════════════════════════════════════════════════════
        // IV. 建構子與生命週期
        // ════════════════════════════════════════════════════════════════

        public TrendGuardTrade() : base()
        {
            #region Initialization
            Credentials.Author = "CapitalFutures Corp.";
            Credentials.Company = "CapitalFutures Corp.";
            Credentials.Copyrights = "CapitalFutures Corp.";
            Credentials.DateOfCreation = new DateTime(2026, 1, 7);
            Credentials.ExpirationDate = new DateTime(2026, 6, 1);
            Credentials.Version = "1.0";
            Credentials.Password = "66b4a6416f59370e942d353f08a9ae36";
            Credentials.ProjectName = "趨勢防線_交易版";
            #endregion
        }

        public override void Init()
        {
            currentAccount = AccountManager.Current;

            if (currentAccount == null || !AllowedAccounts.Contains(currentAccount.Name))
            {
                Notification.Alert("此策略僅限授權帳號使用");
                return;
            }
            
            // 清除舊的信號
            ClearOldSignals();
            
            // 啟動信號監控定時器 (200ms 間隔)
            signalCheckTimer = new System.Timers.Timer(200);
            signalCheckTimer.Elapsed += SignalCheckTimer_Elapsed;
            signalCheckTimer.AutoReset = true;
            signalCheckTimer.Start();
            
            if (enableLogMessage)
            {
                Notification.Print("═══════════════════════════════════");
                Notification.Print("趨勢防線_交易版 已啟動");
                Notification.Print("📡 高速監控模式 - 信號響應延遲 ≤200ms");
                Notification.Print("💡 等待「趨勢防線」指標發送出場訊號");
                Notification.Print($"📌 確認模式: {(requireConfirmation ? "啟用" : "自動執行")}");
                Notification.Print("═══════════════════════════════════");
            }
        }
        
        private void SignalCheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (isProcessingSignal) return;
            
            try
            {
                isProcessingSignal = true;
                CheckAndExecuteExitSignal();
            }
            finally
            {
                isProcessingSignal = false;
            }
        }

        public override void Update(TickStatus args)
        {
            if (currentAccount == null || !AllowedAccounts.Contains(currentAccount.Name))
            {
                return;
            }
            
            // 持續監控出場信號
            CheckAndExecuteExitSignal();
        }

        public override void Complete()
        {
            // 停止並釋放定時器
            if (signalCheckTimer != null)
            {
                signalCheckTimer.Stop();
                signalCheckTimer.Elapsed -= SignalCheckTimer_Elapsed;
                signalCheckTimer.Dispose();
                signalCheckTimer = null;
            }
            
            if (enableLogMessage)
            {
                Notification.Print("趨勢防線_交易版 已停止");
            }
        }

        // ════════════════════════════════════════════════════════════════
        // V. 信號處理
        // ════════════════════════════════════════════════════════════════
        
        private void ClearOldSignals()
        {
            try
            {
                if (GlobalVariableManager.Exists("TrendGuard_Signal"))
                    GlobalVariableManager.Remove("TrendGuard_Signal");
                if (GlobalVariableManager.Exists("TrendGuard_SignalTime"))
                    GlobalVariableManager.Remove("TrendGuard_SignalTime");
            }
            catch { }
        }
        
        private void CheckAndExecuteExitSignal()
        {
            try
            {
                // 強制輸出調試訊息（用於確認策略正在運作）
                // Notification.Print("🔍 策略檢查中...");  // 取消註解可確認策略運作
                
                // 檢查是否有新的出場信號
                bool hasSignal = GlobalVariableManager.Exists("TrendGuard_Signal");
                bool hasSignalTime = GlobalVariableManager.Exists("TrendGuard_SignalTime");
                
                if (!hasSignal || !hasSignalTime)
                {
                    return;
                }
                
                // 發現訊號！
                Notification.Print("🔔 發現 TrendGuard 訊號！");
                
                // 取得信號時間
                long signalTime = (long)GlobalVariableManager.GetValue("TrendGuard_SignalTime");
                
                // 檢查是否是新信號（避免重複處理）
                if (signalTime <= lastProcessedSignalTime)
                {
                    Notification.Print($"⏭️ 訊號已處理過 (signalTime={signalTime}, lastProcessed={lastProcessedSignalTime})");
                    return;
                }
                
                // 標記此信號已處理
                lastProcessedSignalTime = signalTime;
                
                // 取得信號
                string signal = (string)GlobalVariableManager.GetValue("TrendGuard_Signal");
                
                Notification.Print("═══════════════════════════════════");
                Notification.Print("📥 收到新的觸發訊號！");
                Notification.Print($"   Signal: {signal}");
                Notification.Print($"   SignalTime: {signalTime}");
                
                // 清除信號（避免重複觸發）
                GlobalVariableManager.Remove("TrendGuard_Signal");
                GlobalVariableManager.Remove("TrendGuard_SignalTime");
                
                if (signal == "Exit")
                {
                    // 取得詳細資訊
                    string symbol = GlobalVariableManager.Exists("TrendGuard_Symbol") ? 
                        (string)GlobalVariableManager.GetValue("TrendGuard_Symbol") : "";
                    int lineId = GlobalVariableManager.Exists("TrendGuard_LineId") ? 
                        (int)GlobalVariableManager.GetValue("TrendGuard_LineId") : 0;
                    double triggerPrice = GlobalVariableManager.Exists("TrendGuard_TriggerPrice") ? 
                        (double)GlobalVariableManager.GetValue("TrendGuard_TriggerPrice") : 0;
                    double exitPercent = GlobalVariableManager.Exists("TrendGuard_ExitPercent") ? 
                        (double)GlobalVariableManager.GetValue("TrendGuard_ExitPercent") : 100;
                    // 穿越方向: 1=Down(平多), 2=Up(平空)
                    int crossDirectionValue = GlobalVariableManager.Exists("TrendGuard_CrossDirection") ? 
                        (int)GlobalVariableManager.GetValue("TrendGuard_CrossDirection") : 1;
                    
                    string actionText = crossDirectionValue == 1 ? "平多" : "平空";
                    if (enableLogMessage)
                    {
                        Notification.Print($"   Symbol: {symbol}");
                        Notification.Print($"   LineId: {lineId}");
                        Notification.Print($"   TriggerPrice: {triggerPrice:F5}");
                        Notification.Print($"   ExitPercent: {exitPercent:F1}%");
                        Notification.Print($"   動作: {actionText} (CrossDirection={crossDirectionValue})");
                    }
                    
                    // 執行平倉
                    ExecutePartialExit(symbol, lineId, triggerPrice, exitPercent, crossDirectionValue);
                }
                else
                {
                    if (enableLogMessage)
                    {
                        Notification.Print($"   未知信號類型: {signal}");
                    }
                }
            }
            catch (Exception ex)
            {
                Notification.Print($"監控信號錯誤: {ex.Message}");
            }
        }
        
        // crossDirectionValue: 1=Down(平多), 2=Up(平空)
        private void ExecutePartialExit(string symbol, int lineId, double triggerPrice, double exitPercent, int crossDirectionValue)
        {
            try
            {
                // 1=Down→平多單, 2=Up→平空單
                string actionText = crossDirectionValue == 1 ? "平多" : "平空";
                
                if (enableLogMessage)
                {
                    Notification.Print("═══════════════════════════════════");
                    Notification.Print($"🔧 開始執行{actionText}...");
                    Notification.Print($"   Symbol: {symbol}");
                    Notification.Print($"   穿越方向: {(crossDirectionValue == 1 ? "向下(平多)" : "向上(平空)")}");
                }
                
                // 根據商品代碼獲取 Instrument
                var instruments = InstrumentsManager.GetInstruments(symbol);
                Instrument targetInstrument = instruments != null && instruments.Count > 0 ? instruments[0] : null;
                
                if (targetInstrument == null)
                {
                    Notification.Alert($"⚠️ 找不到商品 {symbol}！");
                    return;
                }
                
                if (enableLogMessage)
                {
                    Notification.Print($"   ✓ 找到商品: {targetInstrument.Symbol}");
                }
                
                // 取得所有持倉
                var allPositions = PositionsManager.GetPositions();
                if (enableLogMessage)
                {
                    Notification.Print($"   總持倉數量: {allPositions?.Count ?? 0}");
                    if (allPositions != null)
                    {
                        foreach (var p in allPositions)
                        {
                            Notification.Print($"      - {p.Instrument.Symbol}: {p.Side} x {p.Quantity}");
                        }
                    }
                }
                
                // 取得該商品的持倉 (使用 lambda 表達式過濾)
                var positions = PositionsManager.GetPositions(position => position.Instrument.Symbol == symbol);
                if (positions == null || positions.Count == 0)
                {
                    // 沒有持倉，只顯示通知
                    Notification.Print($"📢 商品 {symbol} 沒有持倉，僅顯示觸發通知");
                    return;
                }
                
                if (enableLogMessage)
                {
                    Notification.Print($"   該商品持倉數: {positions.Count}");
                }
                
                // 計算要平倉的持倉
                double totalQuantity = 0;
                OrderSide exitSide = OrderSide.Sell;  // 預設平多
                List<Position> positionsToClose = new List<Position>();
                
                foreach (var pos in positions)
                {
                    if (enableLogMessage)
                    {
                        Notification.Print($"   檢查持倉: Side={pos.Side}, Qty={pos.Quantity}");
                    }
                    
                    // 根據穿越方向決定要平的持倉
                    // 向下穿越 (1) → 平多單
                    // 向上穿越 (2) → 平空單
                    if (crossDirectionValue == 1 && pos.Side == PositionSide.Long)
                    {
                        // 向下穿越 → 平多單
                        totalQuantity += pos.Quantity;
                        exitSide = OrderSide.Sell;
                        positionsToClose.Add(pos);
                        if (enableLogMessage) Notification.Print($"      → 加入平倉清單 (多單)");
                    }
                    else if (crossDirectionValue == 2 && pos.Side == PositionSide.Short)
                    {
                        // 向上穿越 → 平空單
                        totalQuantity += pos.Quantity;
                        exitSide = OrderSide.Buy;
                        positionsToClose.Add(pos);
                        if (enableLogMessage) Notification.Print($"      → 加入平倉清單 (空單)");
                    }
                }
                
                if (totalQuantity <= 0 || positionsToClose.Count == 0)
                {
                    // 沒有對應方向的持倉
                    Notification.Print($"📢 沒有{(crossDirectionValue == 1 ? "多單" : "空單")}可平，僅顯示觸發通知");
                    return;
                }
                
                // 計算平倉數量
                double exitQuantity = Math.Round(totalQuantity * (exitPercent / 100.0), 2);
                
                // 確保至少平最小交易量
                double minLot = targetInstrument.MinimalLot();
                if (exitQuantity < minLot && totalQuantity >= minLot)
                {
                    exitQuantity = minLot;
                }
                
                // 確保不超過總數量
                if (exitQuantity > totalQuantity)
                {
                    exitQuantity = totalQuantity;
                }
                
                string sideText = exitSide == OrderSide.Buy ? "平空" : "平多";
                
                // 確認對話框
                if (requireConfirmation)
                {
                    string confirmMsg = $"═══ 趨勢防線觸發 ═══\n\n" +
                                       $"觸發線: 防守線 #{lineId}\n" +
                                       $"觸發價: {triggerPrice:F5}\n" +
                                       $"商品: {symbol}\n" +
                                       $"動作: {sideText}\n" +
                                       $"平倉比例: {exitPercent:F1}%\n" +
                                       $"平倉數量: {exitQuantity} / {totalQuantity}\n\n" +
                                       $"確定要執行此平倉嗎？";
                    
                    DialogResult result = MessageBox.Show(
                        confirmMsg, 
                        "趨勢防線 - 平倉確認", 
                        MessageBoxButtons.YesNo, 
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2
                    );
                    
                    if (result != DialogResult.Yes)
                    {
                        Notification.Print("❌ 平倉已取消");
                        return;
                    }
                }
                
                // 執行平倉 - 使用 PositionsManager.Close
                double remainingQty = exitQuantity;
                foreach (var pos in positionsToClose)
                {
                    if (remainingQty <= 0) break;
                    
                    double closeQty = Math.Min(pos.Quantity, remainingQty);
                    
                    // 使用 PositionsManager.Close 來平倉
                    PositionsManager.Close(pos, closeQty);
                    remainingQty -= closeQty;
                    
                    if (enableLogMessage)
                    {
                        Notification.Print($"   平倉 Position ID: {pos.ID}, 數量: {closeQty}");
                    }
                }
                
                if (enableLogMessage)
                {
                    Notification.Print($"═══════════════════════════════════");
                    Notification.Print($"✅ 平倉已執行！");
                    Notification.Print($"   觸發線: 防守線 #{lineId}");
                    Notification.Print($"   商品: {symbol}");
                    Notification.Print($"   動作: {sideText}");
                    Notification.Print($"   數量: {exitQuantity} ({exitPercent:F1}%)");
                    Notification.Print($"   剩餘: {totalQuantity - exitQuantity}");
                    Notification.Print($"═══════════════════════════════════");
                }
            }
            catch (Exception ex)
            {
                Notification.Alert($"平倉錯誤: {ex.Message}");
            }
        }
    }
}
