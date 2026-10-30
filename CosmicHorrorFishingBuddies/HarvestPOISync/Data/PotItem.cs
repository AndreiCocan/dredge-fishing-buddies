using UnityEngine;

namespace CosmicHorrorFishingBuddies.HarvestPOISync.Data
{
	/// <summary>
	/// One item inside a crab pot, as plain fields so other players' data is never run through a deserializer that
	/// can construct arbitrary types.
	/// </summary>
	internal struct PotItem
	{
		public string id;
		public int x;
		public int y;
		public int z;
		public float durability;
		public bool seen;

		public bool isFish;
		public float size;
		public float freshness;
		public bool isInfected;

		public static PotItem From(SpatialItemInstance item)
		{
			var potItem = new PotItem
			{
				id = item.id,
				x = item.x,
				y = item.y,
				z = item.z,
				durability = item.durability,
				seen = item.seen,
			};
			if (item is FishItemInstance fish)
			{
				potItem.isFish = true;
				potItem.size = fish.size;
				potItem.freshness = fish.freshness;
				potItem.isInfected = fish.isInfected;
			}
			return potItem;
		}

		public SpatialItemInstance ToInstance()
		{
			var item = isFish
				? new FishItemInstance { size = size, freshness = freshness, isInfected = isInfected }
				: new SpatialItemInstance();
			item.id = id;
			item.durability = durability;
			item.seen = seen;
			return item;
		}

		public Vector3Int Position => new(x, y, z);
	}

	/// <summary>
	/// Everything other players need to show a crab pot's state. A struct because Mirror cannot sync arrays directly.
	/// </summary>
	internal struct PotContents
	{
		public PotItem[] items;
		public float durability;

		public bool SameAs(PotContents other) =>
			items != null && other.items != null && durability == other.durability && System.Linq.Enumerable.SequenceEqual(items, other.items);
	}
}
