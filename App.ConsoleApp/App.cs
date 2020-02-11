using HtmlAgilityPack;
using Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace App.ConsoleApp
{
    class App
    {
        private static readonly string BaseUrl = "https://www.pokemon.com";
        private static int _numOfPages;
        private static int _numOfCards;

        static void Main(string[] args)
        {
            SetNumberOfCards();

            Console.Write($"There are around {_numOfCards} cards on the game right now. Want to fetch them? (y/n)");
            var shouldFetchCards = Console.ReadLine() == "y";

            if (!shouldFetchCards)
                return;

            var startTime = DateTime.Now;

            Parallel.For(1, 10, i =>
            {
                var pageNode = new HtmlWeb().Load(GetCardListPageUrl(i)).DocumentNode;
                Parallel.ForEach(pageNode.QuerySelectorAll("#cardResults li"), cardListItem =>
                {
                    var cardUrl = string.Concat(BaseUrl, cardListItem.QuerySelector("a").Attributes["href"].Value);
                    var cardPage = new HtmlWeb().Load(cardUrl).DocumentNode;
                    var cardModel = Mapper.CardModelFrom(cardPage);
                    Console.WriteLine($"Fetched card {cardModel.Name}.");
                });
            });

            var secondsEllapsed = (DateTime.Now - startTime).TotalSeconds;
            var average = 5 * 12 / secondsEllapsed;
            Console.WriteLine($"Fetched {Math.Floor(average)} cards/s");

        }

        private static void SetNumberOfCards()
        {
            SetNumberOfPages();
            _numOfCards = 12 * _numOfPages;
        }

        private static void SetNumberOfPages()
        {
            var pageNode = new HtmlWeb().Load(GetCardListPageUrl()).DocumentNode;
            var value = pageNode
                .QuerySelector("#cards-load-more > div > span")
                .InnerText
                .Substring(5);
            _numOfPages = int.Parse(value);
        }

        private static string GetCardListPageUrl(int pageIndex = 1)
        {
            return string.Concat(BaseUrl, $"/us/pokemon-tcg/pokemon-cards/{pageIndex}?cardName=&cardText=&evolvesFrom=&simpleSubmit=&format=unlimited&hitPointsMin=0&hitPointsMax=340&retreatCostMin=0&retreatCostMax=5&totalAttackCostMin=0&totalAttackCostMax=5&particularArtist=");
        }

    }
}