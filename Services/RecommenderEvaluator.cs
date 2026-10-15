using DiscoRec;

namespace EZ_Rent.Services
{
    public static class RecommenderEvaluator
    {
        public static EvaluationResult Evaluate(
            List<(string UserId, string ItemId)> interactions,
            double trainRatio = 0.8,
            int topK = 20)
        {
            var rng = new Random(42);
            var trainList = new List<(string, string)>();
            var testList  = new List<(string, string)>();

            foreach (var item in interactions)
            {
                if (rng.NextDouble() < trainRatio)
                    trainList.Add(item);
                else
                    testList.Add(item);
            }

            var trainDataset = new Dataset<string, string>();
            foreach (var (uid, iid) in trainList)
                trainDataset.Add(uid, iid);

            var model = Recommender<string, string>.FitImplicit(trainDataset);

            // Test theo user
            var testByUser = testList.GroupBy(t => t.Item1).ToList();

            int totalHits = 0;
            int usersWithHit = 0;
            double sumPrecision = 0;
            double sumRecall = 0;

            foreach (var group in testByUser)
            {
                var userId = group.Key;
                var actualItems = group.Select(t => t.Item2).ToHashSet();

                var recs = model.UserRecs(userId, topK);
                if (recs == null) continue;

                var recommendedIds = recs.Select(r => r.Id).Take(topK).ToList();
                var recommendedSet = recommendedIds.ToHashSet();

                // Hits = số item actual nằm trong top-K
                int hits = actualItems.Count(a => recommendedSet.Contains(a));
                totalHits += hits;

                if (hits > 0) usersWithHit++;

                // Per-user Precision & Recall
                sumPrecision += (double)hits / topK;
                sumRecall    += (double)hits / actualItems.Count;
            }

            int usersCount = testByUser.Count;

            return new EvaluationResult
            {
                TrainSize       = trainList.Count,
                TestSize        = testList.Count,
                UsersEvaluated  = usersCount,
                Hits            = totalHits,
                K               = topK,

                // Metrics chuẩn
                PrecisionAtK    = usersCount > 0 ? sumPrecision / usersCount : 0,
                RecallAtK       = usersCount > 0 ? sumRecall / usersCount : 0,
                HitRateAtK      = usersCount > 0 ? (double)usersWithHit / usersCount : 0
            };
        }
    }

    public class EvaluationResult
    {
        public int    TrainSize      { get; set; }
        public int    TestSize       { get; set; }
        public int    UsersEvaluated { get; set; }
        public int    Hits           { get; set; }
        public int    K              { get; set; }
        public double PrecisionAtK   { get; set; }
        public double RecallAtK      { get; set; }
        public double HitRateAtK     { get; set; }
    }
}