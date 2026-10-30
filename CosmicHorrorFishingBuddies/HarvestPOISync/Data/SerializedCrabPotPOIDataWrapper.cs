namespace CosmicHorrorFishingBuddies.HarvestPOISync.Data
{
	// Need to wrap this else the weaver complains if we try to use it in commands/RPC
	internal class SerializedCrabPotPOIDataWrapper
	{
		public float yRotation;
		public float x;
		public float z;
		public string deployableItemId;
		public float durability;
		public float timeUntilNextCatchRoll;
		public float lastUpdate;
		private bool hadDurabilityRemaining;

		public SerializedCrabPotPOIDataWrapper() { }

		public SerializedCrabPotPOIDataWrapper(SerializedCrabPotPOIData data)
		{
			yRotation = data.yRotation;
			x = data.x;	
			z = data.z;
			deployableItemId = data.deployableItemId;
			durability = data.durability;
			timeUntilNextCatchRoll = data.timeUntilNextCatchRoll;
			lastUpdate = data.lastUpdate;
			hadDurabilityRemaining = data.hadDurabilityRemaining;
		}

		public SerializedCrabPotPOIData ToData()
		{
			var item = new SpatialItemInstance();
			item.id = deployableItemId;
			item.durability = durability;

			var crabPot = new SerializedCrabPotPOIData(item, x, z);

			crabPot.timeUntilNextCatchRoll = timeUntilNextCatchRoll;

			crabPot.hadDurabilityRemaining = hadDurabilityRemaining;

			crabPot.yRotation = yRotation;

			return crabPot;
		}
	}
}
