using System;
using HtmlAgilityPack;

namespace Models
{
    public static class Mapper
    {
        public static CardModel CardModelFrom(HtmlNode cardPageNode)
        {
            var cardName = cardPageNode
                .QuerySelector("div.card-description div h1")
                .InnerText;
            var cardImgUrl = cardPageNode
                .QuerySelector("div.card-image img")
                .Attributes["src"].Value;

            var cardStatsFooterElement = cardPageNode.QuerySelector("div.stats-footer");
            var cardCode = cardStatsFooterElement
                .QuerySelector("span")
                .InnerText
                .Substring(0, 3);
            var cardExpansion = cardStatsFooterElement
                .QuerySelector("a")
                .InnerText
                .Replace("amp;", "");

            return new CardModel()
            {
                Code = cardCode,
                Name = cardName,
                Expansion = cardExpansion,
                ImgUrl = cardImgUrl
            };
        }
    }
}