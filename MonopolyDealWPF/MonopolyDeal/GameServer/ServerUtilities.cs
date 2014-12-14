﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameObjects;
using Lidgren.Network;

namespace GameServer
{
    public class ServerUtilities
    {
        // Receive an update from either a client or the server, depending on where this method is called.
        public static object ReceiveMessage( NetIncomingMessage inc, Datatype messageType )
        {
            switch ( messageType )
            {
                case Datatype.UpdateDeck:
                {
                    return new Deck(ReadCards(inc));
                }

                case Datatype.UpdateDiscardPile:
                {
                    return ReadCards(inc);
                }

                case Datatype.UpdatePlayer:
                {
                    return ReadPlayer(inc);
                }

                case Datatype.UpdatePlayerList:
                {
                    // Read the size of the list of players.
                    int size = inc.ReadInt32();

                    List<Player> playerList = new List<Player>();

                    // Read each name in the message and add it to the list.
                    for ( int i = 0; i < size; ++i )
                    {
                        //playerList.Add(new Player(inc.ReadString(), ReadCards(inc), ReadCards(inc)));
                        playerList.Add(ReadPlayer(inc));
                    }

                    return playerList;
                }

                case Datatype.LaunchGame:
                {
                    return ReadTurn(inc);
                }

                case Datatype.EndTurn:
                {
                    return ReadTurn(inc);
                }
            }

            return null;
        }

        public static Turn ReadTurn( NetIncomingMessage inc )
        {
            int currentTurnOwner = inc.ReadInt32();
            int numberOfActions = inc.ReadInt32();

            Turn turn = new Turn(currentTurnOwner, numberOfActions);

            return turn;
        }

        public static Player ReadPlayer( NetIncomingMessage inc )
        {
            // Read the name of the player.
            string name = inc.ReadString();

            // Read the CardsInPlay list.
            List<List<Card>> cardsInPlay = new List<List<Card>>();
            int count = inc.ReadInt32();
            for ( int i = 0; i < count; ++i )
            {
                cardsInPlay.Add(ReadCards(inc));
            }

            // Read the CardsInHand list.
            List<Card> cardsInHand = ReadCards(inc);

            // This first string in the message is the player's name.
            // The first list of cards is the Player's CardsInPlay list.
            // The second list of cards is the Player's CardsInHand list.
            //return new Player(inc.ReadString(), ReadCards(inc), ReadCards(inc));
            return new Player(name, cardsInPlay, cardsInHand);
        }

        // Read in a list of cards from an incoming message.
        public static List<Card> ReadCards( NetIncomingMessage inc )
        {
            // Read the size of the list of cards.
            int size = inc.ReadInt32();

            List<Card> cards = new List<Card>();

            // Read the values of the properties of each card.
            for ( int i = 0; i < size; ++i )
            {
                string name = inc.ReadString();
                CardType type = (CardType)inc.ReadByte();
                string value = inc.ReadString();
                PropertyType color = (PropertyType)inc.ReadByte();
                PropertyType altColor = (PropertyType)inc.ReadByte();
                string uriPath = inc.ReadString();
                string actionID = inc.ReadString();
                bool isFlipped = inc.ReadBoolean();
                cards.Add(new Card(name, type, Convert.ToInt32(value), color, altColor, uriPath, Convert.ToInt32(actionID), isFlipped));
            }

            return cards;
        }

        public static void WriteCards( NetOutgoingMessage outmsg, List<Card> cardList )
        {
            if ( cardList != null )
            {
                // Write the count of the list of cards.
                outmsg.Write(cardList.Count);

                // Write the properties of each card.
                foreach ( Card card in cardList )
                {
                    outmsg.Write(card.Name);
                    outmsg.Write((byte)card.Type);
                    outmsg.Write(card.Value.ToString());
                    outmsg.Write((byte)card.Color);
                    outmsg.Write((byte)card.AltColor);
                    outmsg.Write(card.CardImageUriPath);
                    outmsg.Write(card.ActionID.ToString());
                    outmsg.Write(card.IsFlipped);
                }
            }
        }

        public static void WritePlayer( NetOutgoingMessage outmsg, Player player )
        {
            // Write the Player's name.
            outmsg.Write(player.Name);
            
            // Write the Player's CardsInPlay (which is a list of card lists).
            outmsg.Write(player.CardsInPlay.Count);

            foreach ( List<Card> cardList in player.CardsInPlay )
            {
                WriteCards(outmsg, cardList);
            }

            // Write the Player's CardsInHand.
            WriteCards(outmsg, player.CardsInHand);
        }

        public static void WriteTurn( NetOutgoingMessage outmsg, Turn turn )
        {
            // Write the data related to the current turn.
            outmsg.Write(turn.CurrentTurnOwner);
            outmsg.Write(turn.NumberOfActions);
        }

        // Send an update to either a client or the server, depending on where this method is called.
        public static void SendMessage( NetPeer netPeer, Datatype messageType, object updatedObject = null )
        {
            NetOutgoingMessage outmsg;

            // Create new message
            if ( netPeer is NetServer )
            {
                outmsg = (netPeer as NetServer).CreateMessage();
            }
            else
            {
                outmsg = (netPeer as NetClient).CreateMessage();
            }

            // Write the type of the message that is being sent.
            outmsg.Write((byte)messageType);

            if ( updatedObject != null )
            {
                switch ( messageType )
                {
                    case Datatype.UpdateDeck:
                    {
                        WriteCards(outmsg, (updatedObject as Deck).CardList);

                        break;
                    }

                    case Datatype.UpdateDiscardPile:
                    {
                        WriteCards(outmsg, (updatedObject as List<Card>));
                        break;
                    }

                    case Datatype.UpdatePlayer:
                    {
                        Player player = (Player)updatedObject;

                        WritePlayer(outmsg, player);

                        break;
                    }

                    case Datatype.UpdatePlayerList:
                    {
                        List<Player> players = (List<Player>)updatedObject;

                        // Write the count of players.
                        outmsg.Write(players.Count);

                        // Write the name of each player in the game.
                        foreach ( Player player in players )
                        {
                            WritePlayer(outmsg, player);
                        }
                        break;
                    }

                    case Datatype.LaunchGame:
                    {
                        Turn currentTurn = (Turn)updatedObject;

                        WriteTurn(outmsg, currentTurn);

                        break;
                    }

                    case Datatype.EndTurn:
                    {
                        Turn currentTurn = (Turn)updatedObject;

                        WriteTurn(outmsg, currentTurn);

                        break;
                    }
                }
            }

            // Send it to the server or a client.
            if ( netPeer is NetServer )
            {
                NetServer server = netPeer as NetServer;

                if ( server.Connections.Count > 0 )
                {
                    server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 0);
                }
            }
            else
            {
                (netPeer as NetClient).SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered);
            }
        }
    }
}
