using HtmlAgilityPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace App.ConsoleApp
{
    class App
    {
        static private readonly Dictionary<string, string> _BaseUrls = new Dictionary<string, string>();

        static void Main(string[] args)
        {
            _BaseUrls.Add("cardList", "https://www.pokemon.com/us/pokemon-tcg/pokemon-cards/?cardName=&cardText=&evolvesFrom=&simpleSubmit=&format=unlimited&hitPointsMin=0&hitPointsMax=340&retreatCostMin=0&retreatCostMax=5&totalAttackCostMin=0&totalAttackCostMax=5&particularArtist=");
            _BaseUrls.Add("cardDetail", "https://www.pokemon.com/us/pokemon-tcg/pokemon-cards/");

            var ammountOfPages = _GetAmmountOfCardsPages();
            Console.WriteLine($"There are {ammountOfPages} pages to be scrapped.");
            Console.ReadLine();
        }

        static private int _GetAmmountOfCardsPages()
        {
            var cardListWebDocument = new HtmlWeb().Load(_BaseUrls["cardList"]);
            var element = cardListWebDocument.GetElementbyId("cards-load-more");
            var numOfPages = int.Parse(element.Element("div").Element("span").InnerText.Substring(4));
            return numOfPages; // 12 is the number of cards on a page
        }

        static private int _GetCardsList()
        {
            return 0;
        }

        static private int _GetCardDetail()
        {
            return 0;
        }

    }
}
