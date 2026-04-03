using UnityEngine;

[CreateAssetMenu(menuName = "Game/Entities/Animals/Deer Definition")]
public class DeerDefinition : AnimalDefinition
{
	private void Reset()
	{
		ApplySpawnDefaults(
			SpawnTimeRule.Always,
			new[] { WorldGenTilemap.Biome.Plains, WorldGenTilemap.Biome.Snomy },
			1f);

		wanderRadius = 5f;
		idleTimeRange = new Vector2(0.7f, 1.8f);
		moveTimeRange = new Vector2(1.0f, 2.4f);
		wanderRunChance = 0.25f;

		fleeDuration = 3.5f;
		fleeSpeedMultiplier = 1.7f;
		fleePreferredDistance = 5.5f;
		fleeRetriggerCooldown = 0.05f;
	}
}
