using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DurakServer.Models
{
    public class DeckBox
    {
        private readonly List<Card> _deckList;
        public readonly List<Card> ShuffledDeckList = new List<Card>();

        public DeckBox()
        {
            _deckList = new List<Card>();
            FillDeck();
            Shuffle();
        }
        private void Shuffle()
        {
            var numberOfCards = _deckList.Count;
            var randomNumber = new Random();

            for (var draws = 0; draws < numberOfCards; draws++)
            {
                ShuffledDeckList.Add(DrawCard(randomNumber.Next(0, _deckList.Count)));
            }
        }

        private void FillDeck()
        {
            int rank = 14;

            for (int index = 0; index < 9; index++)
            {
                _deckList.Add(CreateCard(Suit.Club, (Rank)rank));
                _deckList.Add(CreateCard(Suit.Diamond, (Rank)rank));
                _deckList.Add(CreateCard(Suit.Heart, (Rank)rank));
                _deckList.Add(CreateCard(Suit.Spade, (Rank)rank));
                rank--;
            }
        }

        private Card CreateCard(Suit suit, Rank rank)
        {
            return new Card {Suit = suit, Rank = rank};
        }

        public Card DrawCard(int position = 0)
        {
            Card returnCard = _deckList[position];

            _deckList.Remove(returnCard);

            return returnCard;
        }

        public Card DrawCardFromShuf(int position = 0)
        {
            Card returnCard = ShuffledDeckList[position];

            if (returnCard == null)
                return null;

            ShuffledDeckList.Remove(returnCard);

            return returnCard;
        }

        public Suit GetTrumpSuit()
        {
            int index = ShuffledDeckList.Count - 1;

            return ShuffledDeckList[index].Suit;
        }

        public Card GetTrumpCard()
        {
            return ShuffledDeckList[^1];
        }
    }
}
