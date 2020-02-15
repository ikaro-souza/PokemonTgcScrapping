﻿using System;
using HtmlAgilityPack;

namespace Models
{
    public static class Mapper
    {
        public static CardModel CardModelFrom(HtmlDocument cardPage, string cardUrl)
        {
            var cardName = cardPage
                .QuerySelector("div.card-description div h1")
                .InnerText;
            
            var cardImgUrl = cardPage
                .QuerySelector("div.card-image img")
                .Attributes["src"].Value;

            var cardStatsFooterElement = cardPage.QuerySelector("div.stats-footer");
            var cardCode = cardStatsFooterElement
                .QuerySelector("span")
                .InnerText;
            var cardExpansion = cardStatsFooterElement
                .QuerySelector("a")
                .InnerText
                .Replace("amp;", "");

            return new CardModel()
            {
                Code = cardCode,
                Name = cardName,
                Expansion = cardExpansion,
                ImgUrl = cardImgUrl,
                CardUrl = cardUrl
            };
        }
    }
}