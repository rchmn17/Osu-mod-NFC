// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Database;
using Realms;

namespace osu.Game.IO
{
    // ==========================================================
    // 1. TABEL DATABASE KHUSUS NFC
    // Kita buat tabel baru di Realm khusus untuk pengguna NFC
    // ==========================================================
    public class NFCProfile : RealmObject
    {
        [PrimaryKey]
        public string CardId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }

    // ==========================================================
    // 2. DTO (Data Transfer Object) AMAN UNTUK UI
    // Mencegah error "Realm Threading" saat mengirim data ke layar UI
    // ==========================================================
    public class NFCUserInfo
    {
        public string CardId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }

    public partial class NFCAuthManager : Component
    {
        private readonly RealmAccess realm;

        public event Action<NFCUserInfo>? OnCardScanned;
        public event Action? OnCardRemoved;

        public Bindable<NFCUserInfo?> ActiveNFCUser { get; } = new Bindable<NFCUserInfo?>();
        public Bindable<string?> CurrentCardId { get; } = new Bindable<string?>();

        public NFCAuthManager(RealmAccess realm)
        {
            this.realm = realm;
        }

        public void ProcessCardTap(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                return;

            // Jika kartu yang sama ditempel lagi, abaikan
            if (CurrentCardId.Value == cardId && ActiveNFCUser.Value != null)
                return;

            realm.Run(r =>
            {
                // Cari profil berdasarkan CardId di tabel khusus NFCProfile
                var profile = r.All<NFCProfile>().FirstOrDefault(p => p.CardId == cardId);

                if (profile == null)
                {
                    // Jika belum ada, buat record baru di database lokal
                    string shortId = cardId.Length >= 4 ? cardId[^4..] : cardId;
                    
                    r.Write(() =>
                    {
                        profile = r.Add(new NFCProfile
                        {
                            CardId = cardId,
                            Username = $"Player_{shortId}"
                        });
                    });
                }

                // Buat salinan data yang aman (terpisah dari database) untuk UI
                var userInfo = new NFCUserInfo
                {
                    CardId = profile.CardId,
                    Username = profile.Username
                };

                // Update State & Jalankan Event Pop-up
                CurrentCardId.Value = cardId;
                ActiveNFCUser.Value = userInfo;

                OnCardScanned?.Invoke(userInfo);
            });
        }

        public void ProcessCardRemove()
        {
            if (CurrentCardId.Value == null)
                return;

            CurrentCardId.Value = null;
            ActiveNFCUser.Value = null;

            OnCardRemoved?.Invoke();
        }
    }
}