﻿using Runtime.Script;
using TradeApi.Indicators;
using TradeApi.History;
using System.Drawing;

namespace TestEnv
{
    public class GMMA : IndicatorBuilder
    {
        public GMMA() : base()
        {
            #region Initialization
            Credentials.ProjectName = "GMMA - Guppy Multiple Moving Average";
            Credentials.Description = "由12條EMA組成的複合移動平均指標，分為短期組和長期組";
            Credentials.Author = "EvoCode Developer";
            Credentials.DateOfCreation = new System.DateTime(2025, 11, 3);
            #endregion

            // 短期組 - 淺藍色
            Lines.Set("短期_EMA_03");
            Lines["短期_EMA_03"].Color = Color.DeepSkyBlue;
            Lines["短期_EMA_03"].Width = 1;
            
            Lines.Set("短期_EMA_05");
            Lines["短期_EMA_05"].Color = Color.DeepSkyBlue;
            Lines["短期_EMA_05"].Width = 1;
            
            Lines.Set("短期_EMA_08");
            Lines["短期_EMA_08"].Color = Color.DeepSkyBlue;
            Lines["短期_EMA_08"].Width = 1;
            
            Lines.Set("短期_EMA_10");
            Lines["短期_EMA_10"].Color = Color.DeepSkyBlue;
            Lines["短期_EMA_10"].Width = 1;
            
            Lines.Set("短期_EMA_12");
            Lines["短期_EMA_12"].Color = Color.DeepSkyBlue;
            Lines["短期_EMA_12"].Width = 2;  // 較粗
            
            Lines.Set("短期_EMA_15");
            Lines["短期_EMA_15"].Color = Color.DeepSkyBlue;
            Lines["短期_EMA_15"].Width = 1;
            
            // 長期組 - 紅色
            Lines.Set("長期_EMA_30");
            Lines["長期_EMA_30"].Color = Color.Red;
            Lines["長期_EMA_30"].Width = 1;
            
            Lines.Set("長期_EMA_35");
            Lines["長期_EMA_35"].Color = Color.Red;
            Lines["長期_EMA_35"].Width = 1;
            
            Lines.Set("長期_EMA_40");
            Lines["長期_EMA_40"].Color = Color.Red;
            Lines["長期_EMA_40"].Width = 1;
            
            Lines.Set("長期_EMA_45");
            Lines["長期_EMA_45"].Color = Color.Red;
            Lines["長期_EMA_45"].Width = 1;
            
            Lines.Set("長期_EMA_50");
            Lines["長期_EMA_50"].Color = Color.Red;
            Lines["長期_EMA_50"].Width = 2;  // 較粗
            
            Lines.Set("長期_EMA_60");
            Lines["長期_EMA_60"].Color = Color.Red;
            Lines["長期_EMA_60"].Width = 1;

            base.SeparateWindow = false;
        }
        
        // 固定週期陣列
        private int[] periods = new int[] { 3, 5, 8, 10, 12, 15, 30, 35, 40, 45, 50, 60 };
        
        // 對應的線條名稱
        private string[] lineNames = new string[] { 
            "短期_EMA_03", "短期_EMA_05", "短期_EMA_08", "短期_EMA_10", "短期_EMA_12", "短期_EMA_15",
            "長期_EMA_30", "長期_EMA_35", "長期_EMA_40", "長期_EMA_45", "長期_EMA_50", "長期_EMA_60"
        };
        
        // 儲存EMA指標
        private BuiltInIndicator[] emas = new BuiltInIndicator[12];
        
        public override void Init()
        {
            // 建立12條EMA
            for (int i = 0; i < 12; i++)
            {
                emas[i] = IndicatorsManager.BuildIn.MA(
                    HistoryDataSeries, 
                    periods[i], 
                    MAMode.EMA, 
                    0, 
                    PriceType.Close
                );
            }
            
            // 移除提醒訊息
            // Notification.Alert("GMMA 指標已載入：12條EMA均線");
        }
        
        public override void Update(TickStatus args)
        {
            // 更新所有線條的數值
            for (int i = 0; i < 12; i++)
            {
                Lines[lineNames[i]].SetValue(emas[i].GetValue());
            }
        }
    }
}