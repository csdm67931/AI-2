﻿using Runtime.Script;
using Runtime.Script.Charts;
using System;
using System.Drawing;
using TradeApi.History;
using TradeApi.Indicators;

namespace AdaptiveTrendArrow
{
    public class AdaptiveTrendArrow : IndicatorBuilder
    {
        #region 建構式
        public AdaptiveTrendArrow() : base()
        {
            #region Initialization
            Credentials.ProjectName = "自適應趨勢箭頭指標";
            Credentials.Description = "動態支撐阻力與趨勢突破信號";
            Credentials.Author = "Bob";
            Credentials.DateOfCreation = DateTime.Now;
            #endregion

            // 建立多方突破信號箭頭 (向上)
            Lines.Set("SignUp");
            Lines["SignUp"].Style = LineStyle.Symbol;
            Lines["SignUp"].ArrowCode = 0233;
            Lines["SignUp"].Color = Color.Red;
            Lines["SignUp"].Width = 5;

            // 建立空方跌破信號箭頭 (向下)
            Lines.Set("SignDown");
            Lines["SignDown"].Style = LineStyle.Symbol;
            Lines["SignDown"].ArrowCode = 0234;
            Lines["SignDown"].Color = Color.Lime;
            Lines["SignDown"].Width = 5;

            // 建立動態阻力線 (上方紅線)
            Lines.Set("Resistance");
            Lines["Resistance"].Color = Color.Red;
            Lines["Resistance"].Width = 2;

            // 建立動態支撐線 (下方綠線)
            Lines.Set("Support");
            Lines["Support"].Color = Color.Lime;
            Lines["Support"].Width = 2;

            SeparateWindow = false;
        }
        #endregion

        #region 輸入參數
        [InputParameter(InputType.Numeric, "主要計算週期", 0)]
        [SimpleNumeric(1D, 99999D)]
        public int CalcPeriod = 15;

        [InputParameter(InputType.Numeric, "ATR週期", 1)]
        [SimpleNumeric(1D, 99999D)]
        public int ATRPeriod = 15;

        [InputParameter(InputType.Checkbox, "顯示支撐阻力線", 2)]
        public bool ShowLines = true;

        [InputParameter(InputType.Checkbox, "顯示信號箭頭", 3)]
        public bool ShowArrows = true;
        #endregion

        #region 內部變數
        // ATR 指標
        private BuiltInIndicator atr;
        
        // 每週期K線數量
        private int barsPerPeriod;
        
        // 最小需要的K線數量
        private int minBarsRequired;
        
        // 趨勢狀態變數
        private double prevTrendUp = 0.0;
        private double prevTrendDown = 0.0;
        
        // 是否已完成歷史回補
        private bool historyCalculated = false;
        
        // ATR 倍數 (用於箭頭位置計算)
        private const int ATR_NUM = 3;
        private const int ATR_DEN = 8;
        #endregion

        #region Init
        public override void Init()
        {
            // 計算每週期K線數量
            barsPerPeriod = GetBarsPerPeriod();
            
            // 計算最小需要的K線數量
            minBarsRequired = barsPerPeriod * (CalcPeriod + 2);
            minBarsRequired = Math.Max(minBarsRequired, ATRPeriod + 10);
            
            // 建立ATR指標
            atr = IndicatorsManager.BuildIn.ATR(HistoryDataSeries, ATRPeriod);
            
            // 設定指標名稱
            ScriptShortName = string.Format("AdaptiveTrend({0},{1})", CalcPeriod, ATRPeriod);
            
            // 重置歷史計算標記
            historyCalculated = false;
            prevTrendUp = 0.0;
            prevTrendDown = 0.0;
            
//            Notification.Alert(string.Format("指標初始化完成 - 每週期K線數:{0}, 最小K線需求:{1}", 
//                barsPerPeriod, minBarsRequired));
        }
        #endregion

        #region Update
        public override void Update(TickStatus args)
        {
            // 數據充足性檢查
            if (HistoryDataSeries.Count < minBarsRequired)
                return;

            // 首次執行時回補歷史計算
            if (!historyCalculated)
            {
                CalculateHistory();
                historyCalculated = true;
            }

            // 計算當前K棒
            CalculateBar(0);
        }
        #endregion

        #region 歷史回補計算
        private void CalculateHistory()
        {
            // 重置趨勢狀態
            prevTrendUp = 0.0;
            prevTrendDown = 0.0;

            // 從最早的有效K棒開始計算到當前
            int startBar = HistoryDataSeries.Count - minBarsRequired;
            if (startBar < 0) startBar = 0;

            // 由舊到新計算 (index 大 = 較舊)
            for (int bar = startBar; bar >= 0; bar--)
            {
                CalculateBar(bar);
            }
        }
        #endregion

        #region 單根K棒計算
        private void CalculateBar(int bar)
        {
            // 計算支撐阻力位
            double HH = AverageHigh_Simple(bar, CalcPeriod);
            double LL = AverageLow_Simple(bar, CalcPeriod);

            // 檢查計算結果是否有效
            if (HH <= 0 || LL <= 0 || HH <= LL)
                return;

            // 繪製支撐阻力線
            if (ShowLines)
            {
                Lines["Resistance"].SetValue(HH, bar);
                Lines["Support"].SetValue(LL, bar);
            }

            // 取得當前收盤價
            double close = HistoryDataSeries.GetValue(PriceType.Close, bar);

            // 趨勢判斷
            double TrendUp = 0.0;
            double TrendDown = 0.0;

            if (close > HH)
            {
                TrendUp = LL;
            }
            else if (close < LL)
            {
                TrendDown = HH;
            }
            else
            {
                if (prevTrendDown != 0) TrendDown = HH;
                if (prevTrendUp != 0) TrendUp = LL;
            }

            // 信號產生
            if (ShowArrows)
            {
                double atrValue = atr.GetValue(bar);
                
                if (atrValue > 0)
                {
                    // 多方突破信號 (TrendUp 從 0 變為非 0)
                    if (prevTrendUp == 0 && TrendUp != 0)
                    {
                        double arrowPrice = HistoryDataSeries.GetValue(PriceType.Low, bar) 
                                          - atrValue * ATR_NUM / ATR_DEN;
                        Lines["SignUp"].SetValue(arrowPrice, bar);
                    }

                    // 空方跌破信號 (TrendDown 從 0 變為非 0)
                    if (prevTrendDown == 0 && TrendDown != 0)
                    {
                        double arrowPrice = HistoryDataSeries.GetValue(PriceType.High, bar) 
                                          + atrValue * ATR_NUM / ATR_DEN;
                        Lines["SignDown"].SetValue(arrowPrice, bar);
                    }
                }
            }

            // 保存當前趨勢狀態供下一根K棒使用
            prevTrendUp = TrendUp;
            prevTrendDown = TrendDown;
        }
        #endregion

        #region 輔助函數 - 取得每週期K線數量
        private int GetBarsPerPeriod()
        {
            // 嘗試取得時間週期資訊
            if (HistoryDataSeries.HistoricalRequest is TimeHistoricalRequest timeReq)
            {
                Period period = timeReq.Period;
                int value = timeReq.Value;

                switch (period)
                {
                    case Period.Minute:
                        if (value == 1) return 60;       // M1: 1小時 = 60根
                        if (value == 2) return 30;       // M2: 1小時 = 30根
                        if (value == 3) return 20;       // M3: 1小時 = 20根
                        if (value == 4) return 15;       // M4: 1小時 = 15根
                        if (value == 5) return 12;       // M5: 1小時 = 12根
                        if (value == 6) return 10;       // M6: 1小時 = 10根
                        if (value == 10) return 6;       // M10: 1小時 = 6根
                        if (value == 12) return 5;       // M12: 1小時 = 5根
                        if (value == 15) return 4;       // M15: 1小時 = 4根
                        if (value == 20) return 3;       // M20: 1小時 = 3根
                        if (value == 30) return 8;       // M30: 4小時 = 8根
                        return Math.Max(1, 60 / value);
                        
                    case Period.Hour:
                        if (value == 1) return 4;        // H1: 4小時 = 4根
                        if (value == 2) return 2;        // H2: 4小時 = 2根
                        if (value == 3) return 2;        // H3: 6小時約2根
                        if (value == 4) return 6;        // H4: 1天 = 6根
                        if (value == 6) return 4;        // H6: 1天 = 4根
                        if (value == 8) return 3;        // H8: 1天 = 3根
                        if (value == 12) return 2;       // H12: 1天 = 2根
                        return Math.Max(1, 24 / value);
                        
                    case Period.Day:
                        return 5;                        // D1: 1週 = 5根
                        
                    case Period.Week:
                        return 4;                        // W1: 1月約4根
                        
                    case Period.Month:
                        return 12;                       // MN: 1年 = 12根
                        
                    default:
                        return 10;
                }
            }
            
            // 預設值
            return 10;
        }
        #endregion

        #region 輔助函數 - 計算平均高點
        private double AverageHigh_Simple(int index, int period)
        {
            // 計算回溯的K線數量
            int lookback = period * barsPerPeriod;

            // 邊界檢查
            if (index + lookback >= HistoryDataSeries.Count)
                return 0.0;

            double sum = 0.0;

            // 將回溯區間分成 period 個段落，取每段最高點
            for (int i = 0; i < period; i++)
            {
                int startPos = index + i * barsPerPeriod;
                double segmentHigh = double.MinValue;

                // 找出該段落的最高點
                for (int j = 0; j < barsPerPeriod; j++)
                {
                    int pos = startPos + j;
                    if (pos >= HistoryDataSeries.Count)
                        return 0.0;

                    double high = HistoryDataSeries.GetValue(PriceType.High, pos);
                    if (high > segmentHigh)
                        segmentHigh = high;
                }

                if (segmentHigh == double.MinValue)
                    return 0.0;

                sum += segmentHigh;
            }

            return sum / period;
        }
        #endregion

        #region 輔助函數 - 計算平均低點
        private double AverageLow_Simple(int index, int period)
        {
            // 計算回溯的K線數量
            int lookback = period * barsPerPeriod;

            // 邊界檢查
            if (index + lookback >= HistoryDataSeries.Count)
                return 0.0;

            double sum = 0.0;

            // 將回溯區間分成 period 個段落，取每段最低點
            for (int i = 0; i < period; i++)
            {
                int startPos = index + i * barsPerPeriod;
                double segmentLow = double.MaxValue;

                // 找出該段落的最低點
                for (int j = 0; j < barsPerPeriod; j++)
                {
                    int pos = startPos + j;
                    if (pos >= HistoryDataSeries.Count)
                        return 0.0;

                    double low = HistoryDataSeries.GetValue(PriceType.Low, pos);
                    if (low < segmentLow)
                        segmentLow = low;
                }

                if (segmentLow == double.MaxValue)
                    return 0.0;

                sum += segmentLow;
            }

            return sum / period;
        }
        #endregion
    }
}