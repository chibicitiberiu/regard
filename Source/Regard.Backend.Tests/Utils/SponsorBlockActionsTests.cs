using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Configuration;
using Regard.Common.SponsorBlock;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Backend.Tests.Utils
{
    /// <summary>
    /// Covers the shipped default and the "none" sentinel that came with it. The two are joined at the
    /// hip: the moment the option's default stopped being empty, an empty stored value started meaning
    /// "unset, fall back to the default", which left no way to say "off" without a sentinel.
    /// </summary>
    [TestClass]
    public class SponsorBlockActionsTests
    {
        // --- the shipped default -------------------------------------------------------------------

        [TestMethod]
        public void The_shipped_default_skips_sponsor_in_the_player()
        {
            var map = SponsorBlockActions.Parse(SponsorBlockActions.DefaultActions);

            Assert.AreEqual(1, map.Count, "SponsorBlock's own default enables exactly one category");
            Assert.AreEqual(SbAction.Skip, map["sponsor"]);
        }

        [TestMethod]
        public void The_shipped_default_leaves_every_other_category_alone()
        {
            var skipped = SponsorBlockActions.CategoriesWith(SponsorBlockActions.DefaultActions, SbAction.Skip);

            CollectionAssert.AreEqual(new[] { "sponsor" }, skipped.ToArray());
            Assert.AreEqual(0, SponsorBlockActions.CategoriesWith(SponsorBlockActions.DefaultActions, SbAction.Remove).Count,
                            "the default must never cut a file");
            Assert.AreEqual(0, SponsorBlockActions.CategoriesWith(SponsorBlockActions.DefaultActions, SbAction.Chapter).Count);
        }

        [TestMethod]
        public void The_option_ships_that_default()
        {
            Assert.AreEqual(SponsorBlockActions.DefaultActions, Options.Sponsorblock_Actions.DefaultValue);
        }

        [TestMethod]
        public void The_default_round_trips_through_serialize()
        {
            var map = SponsorBlockActions.Parse(SponsorBlockActions.DefaultActions);
            Assert.AreEqual(SponsorBlockActions.DefaultActions, SponsorBlockActions.Serialize(map));
        }

        // --- the "none" sentinel -------------------------------------------------------------------

        [TestMethod]
        public void An_all_keep_map_serializes_to_the_none_sentinel_not_to_empty()
        {
            // Empty would be stored as "unset" and silently resolve back to the default, so turning
            // everything off in the UI would appear to do nothing.
            Assert.AreEqual(SponsorBlockActions.None, SponsorBlockActions.Serialize(new Dictionary<string, SbAction>()));
        }

        [TestMethod]
        public void Dropping_the_last_category_yields_the_sentinel()
        {
            var map = SponsorBlockActions.Parse(SponsorBlockActions.DefaultActions);
            map.Remove("sponsor");

            Assert.AreEqual(SponsorBlockActions.None, SponsorBlockActions.Serialize(map));
        }

        [TestMethod]
        public void The_sentinel_parses_back_to_nothing_enabled()
        {
            Assert.AreEqual(0, SponsorBlockActions.Parse(SponsorBlockActions.None).Count);
            Assert.IsFalse(SponsorBlockActions.Any(SponsorBlockActions.None));
            Assert.AreEqual(0, SponsorBlockActions.CategoriesWith(SponsorBlockActions.None, SbAction.Skip).Count);
        }

        [TestMethod]
        public void The_sentinel_is_not_mistaken_for_a_category()
        {
            CollectionAssert.DoesNotContain(SponsorBlockActions.Categories, SponsorBlockActions.None);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(null)]
        [DataRow("   ")]
        public void Empty_still_parses_to_nothing_so_a_stored_blank_is_harmless(string csv)
        {
            // The distinction between blank and "none" lives in the option store (blank = unset), not
            // here — Parse treats both as "no categories enabled".
            Assert.AreEqual(0, SponsorBlockActions.Parse(csv).Count);
        }

        // --- unchanged behaviour the above must not have broken ------------------------------------

        [TestMethod]
        public void A_populated_map_still_serializes_in_canonical_order()
        {
            var map = new Dictionary<string, SbAction>
            {
                ["music_offtopic"] = SbAction.Skip,
                ["sponsor"] = SbAction.Remove,
                ["intro"] = SbAction.Chapter,
            };

            Assert.AreEqual("sponsor:remove,intro:chapter,music_offtopic:skip", SponsorBlockActions.Serialize(map));
        }

        [TestMethod]
        public void An_explicit_keep_entry_is_dropped_rather_than_emitted()
        {
            var map = new Dictionary<string, SbAction>
            {
                ["sponsor"] = SbAction.Skip,
                ["intro"] = SbAction.Keep,
            };

            Assert.AreEqual("sponsor:skip", SponsorBlockActions.Serialize(map));
        }

        [TestMethod]
        public void Remove_and_skip_together_are_still_flagged_as_a_conflict()
        {
            Assert.IsTrue(SponsorBlockActions.HasRemoveSkipConflict("sponsor:remove,intro:skip"));
            Assert.IsFalse(SponsorBlockActions.HasRemoveSkipConflict(SponsorBlockActions.DefaultActions));
            Assert.IsFalse(SponsorBlockActions.HasRemoveSkipConflict(SponsorBlockActions.None));
        }

        [TestMethod]
        public void Every_category_has_a_label_for_the_watch_page_list()
        {
            // The segment panel renders Labels[category] for every segment SponsorBlock returns, and it
            // asks for exactly this list.
            foreach (var category in SponsorBlockActions.Categories)
                Assert.IsTrue(SponsorBlockActions.Labels.ContainsKey(category), category);
        }
    }
}
