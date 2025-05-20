﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace YgoMaster
{
    partial class GameServer
    {
        void WriteUser(GameServerWebRequest request)
        {
            Dictionary<uint, FriendState> friends = GetFriends(request.Player);
            request.Response["User"] = new Dictionary<string, object>()
            {
                { "profile", new Dictionary<string, object>() {
                    { "name", request.Player.Name },
                    { "rank", request.Player.Rank },
                    { "rate", request.Player.Rate },
                    { "pcode", request.Player.Code },
                    { "level", request.Player.Level },
                    { "exp", request.Player.Exp },
                    { "need_exp", 0 },
                    { "icon_id", request.Player.IconId },
                    { "icon_frame_id", request.Player.IconFrameId },
                    { "follow_num", friends.Count(x => x.Value.HasFlag(FriendState.Following)) },
                    { "follower_num", friends.Count(x => x.Value.HasFlag(FriendState.Follower)) },
                    { "avatar_id", request.Player.AvatarId },
                    { "wallpaper", request.Player.Wallpaper },
                    { "tag", request.Player.TitleTags.ToArray() },
                    { "wallpaper_home", request.Player.WallpaperHome.ToArray() },
                }}
            };
        }

        Dictionary<string, object> GetItemHaveDictionary(Player player)
        {
            Dictionary<string, object> have = new Dictionary<string, object>();
            foreach (int item in player.Items)
            {
                have[item.ToString()] = 1;
            }
            // EDITED
            // foreach (int value in ItemID.Values[ItemID.Category.PROFILE_TAG])
            // {
            //     have[value.ToString()] = 1;
            // }
            // END EDITED
            have[((int)ItemID.Value.Gem).ToString()] = player.Gems;
            player.CraftPoints.ToDictionary(have);
            player.OrbPoints.ToDictionary(have);
            return have;
        }

        void WriteItem(GameServerWebRequest request)
        {
            request.Response["Item"] = new Dictionary<string, object>()
            {
                { "have", GetItemHaveDictionary(request.Player) },
            };
        }

        void WriteItem(GameServerWebRequest request, int itemId)
        {
            if (ItemID.GetCategoryFromID(itemId) == ItemID.Category.CARD)
            {
                WriteCards_have(request, itemId);
                return;
            }
            Dictionary<string, object> item = request.GetOrCreateDictionary("Item");
            Dictionary<string, object> have = Utils.GetOrCreateDictionary(item, "have");
            switch ((ItemID.Value)itemId)
            {
                case ItemID.Value.Gem: have[itemId.ToString()] = request.Player.Gems; break;
                case ItemID.Value.CpN: have[itemId.ToString()] = request.Player.CraftPoints.Get(CardRarity.Normal); break;
                case ItemID.Value.CpR: have[itemId.ToString()] = request.Player.CraftPoints.Get(CardRarity.Rare); break;
                case ItemID.Value.CpSR: have[itemId.ToString()] = request.Player.CraftPoints.Get(CardRarity.SuperRare); break;
                case ItemID.Value.CpUR: have[itemId.ToString()] = request.Player.CraftPoints.Get(CardRarity.UltraRare); break;
                case ItemID.Value.OrbDark: have[itemId.ToString()] = request.Player.OrbPoints.Get(OrbType.Dark); break;
                case ItemID.Value.OrbLight: have[itemId.ToString()] = request.Player.OrbPoints.Get(OrbType.Light); break;
                case ItemID.Value.OrbEarth: have[itemId.ToString()] = request.Player.OrbPoints.Get(OrbType.Earth); break;
                case ItemID.Value.OrbWater: have[itemId.ToString()] = request.Player.OrbPoints.Get(OrbType.Water); break;
                case ItemID.Value.OrbFire: have[itemId.ToString()] = request.Player.OrbPoints.Get(OrbType.Fire); break;
                case ItemID.Value.OrbWind: have[itemId.ToString()] = request.Player.OrbPoints.Get(OrbType.Wind); break;
                default:
                    have[itemId.ToString()] = request.Player.Items.Contains(itemId) ? 1 : 0;
                    break;
            }

        }

        void WriteDeckRemoval(GameServerWebRequest request, int id, bool deckNotFound = false)
        {
            if (deckNotFound)
            {
                Utils.LogWarning("Client sent an invalid deck id " + id + ". Removing it.");
            }
            WriteDeck_num_empty(request);
            request.Remove("Deck.list." + id);
            request.Remove("Deck.last_set");// Not required for "Deck.delete_deck_multi"?
            request.Remove("DeckList." + id);
        }

        void WriteDeck(GameServerWebRequest request)
        {
            Dictionary<string, object> deck = request.GetOrCreateDictionary("Deck");
            Dictionary<string, object> list = Utils.GetOrCreateDictionary(deck, "list");
            foreach (DeckInfo deckInfo in request.Player.Decks.Values)
            {
                WriteDeck_list_item(request, list, deckInfo);
            }
            deck["deckMax"] = NumDeckSlots;// Probably a max slot limit when they introduce buying more slots? (deckMax:50 deckLimit:20)
            deck["deckLimit"] = NumDeckSlots;// The deck limit displayed on the top right of the deck selection screen
            WriteDeck_num_empty(request);
        }

        void WriteDeck(GameServerWebRequest request, int id)
        {
            DeckInfo deckInfo;
            if (!request.Player.Decks.TryGetValue(id, out deckInfo))
            {
                return;
            }
            Dictionary<string, object> deck = request.GetOrCreateDictionary("Deck");
            Dictionary<string, object> list = Utils.GetOrCreateDictionary(deck, "list");
            WriteDeck_list_item(request, list, deckInfo);
            WriteDeck_num_empty(request);
            WriteDeck_last_set(request, id);
        }

        void WriteDeck_list_item(GameServerWebRequest request, Dictionary<string, object> list, DeckInfo deckInfo)
        {
            list[deckInfo.Id.ToString()] = new Dictionary<string, object>()
            {
                { "deck_id", deckInfo.Id },
                { "name", deckInfo.Name },
                { "status", 0 },
                { "et", deckInfo.TimeEdited },
                { "ct", deckInfo.TimeCreated },
                { "regulation_id", deckInfo.RegulationId },
                { "accessory", deckInfo.Accessory.ToDictionary() },
                { "pick_cards", deckInfo.DisplayCards.ToIndexDictionary() }
            };
        }

        void WriteDeck_num_empty(GameServerWebRequest request)
        {
            Dictionary<string, object> deck = request.GetOrCreateDictionary("Deck");
            deck["num"] = request.Player.Decks.Count;
            deck["empty"] = Math.Max(0, NumDeckSlots - request.Player.Decks.Count);
        }

        void WriteDeck_last_set(GameServerWebRequest request, int id)
        {
            Dictionary<string, object> deck = request.GetOrCreateDictionary("Deck");
            deck["last_set"] = id;
        }

        void WriteDeckList(GameServerWebRequest request, int deckId)
        {
            Dictionary<string, object> deckList = request.GetOrCreateDictionary("DeckList");
            DeckInfo deckInfo;
            if (request.Player.Decks.TryGetValue(deckId, out deckInfo))
            {
                deckList[deckId.ToString()] = deckInfo.ToDictionary();
            }
        }

        void WriteCards(GameServerWebRequest request)
        {
            WriteCards_have(request);
            WriteCards_favorite(request);
            WriteCards_lock(request);
        }

        void WriteCards_have(GameServerWebRequest request)
        {
            WriteCards_have(request, new HashSet<int>(request.Player.Cards.GetIDs()));
        }

        void WriteCards_have(GameServerWebRequest request, int cardId)
        {
            WriteCards_have(request, new HashSet<int>() { cardId });
        }

        void WriteCards_have(GameServerWebRequest request, HashSet<int> cardIds)
        {
            Dictionary<string, object> cards = request.GetOrCreateDictionary("Cards");
            Dictionary<string, object> ownedCards = Utils.GetOrCreateDictionary(cards, "have");
            foreach (int cardId in cardIds)
            {
                Dictionary<string, object> cardData = request.Player.Cards.CardToDictionary(cardId);
                if (cardData != null)
                {
                    ownedCards[cardId.ToString()] = cardData;
                }
            }
        }

        void WriteCards_favorite(GameServerWebRequest request)
        {
            Dictionary<string, object> cards = request.GetOrCreateDictionary("Cards");
            cards["favorite"] = request.Player.CardFavorites.ToDictionary();
        }

        void WriteCards_lock(GameServerWebRequest request)
        {
            Dictionary<string, object> cards = request.GetOrCreateDictionary("Cards");
            cards["lock"] = request.Player.CardLock.ToDictionary();
        }

        void WriteSolo_deck_info(GameServerWebRequest request)
        {
            DeckInfo deck = request.Player.Duel.GetDeck(GameMode.SoloSingle);
            Dictionary<string, object> solo = request.GetOrCreateDictionary("Solo");
            solo["deck_info"] = new Dictionary<string, object>()
            {
                { "deck_id", deck != null ? deck.Id : 0 },
                { "valid", deck != null ? DisableDeckValidation || deck.IsValid(request.Player, DeckInfo.DefaultRegulationId, Regulation) : false },
                { "possession", true }//request.Player.Duel.IsMyDeck }
            };
        }

        void WritePerPackRarities(GameServerWebRequest request, Dictionary<int, int> cardRare)
        {
            Dictionary<string, object> master = request.GetOrCreateDictionary("Master");
            master["CardRare"] = cardRare;
            request.Remove("Gacha.cardList");
        }

        void WriteToken(GameServerWebRequest request)
        {
            if (!MultiplayerEnabled && string.IsNullOrEmpty(request.Player.Token))
            {
                request.Player.Token = request.Player.Code.ToString();
            }
            request.Response["Persistence"] = new Dictionary<string, object>()
            {
                { "System", new Dictionary<string, object>() {
                    { "token", Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Player.Token)) },
                    { "pcode", request.Player.Code },
                }},
            };
            request.Player.HasWrittenToken = true;
        }
    }
}
