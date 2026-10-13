using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

// Server-only breeding tweaks for the creatures the game breeds itself (Procreation: boar, wolf, lox,
// hen, asksvin, ...), for vanilla and crossplay clients:
//  - Babies stay inside the pen: born where the mother stands (BirthAtParent), then moved between the
//    mother and her nearest tamed partner (BirthBetweenParents).
//  - A MaxCreatures limit per creature.
// Breeding runs on the client that owns the parent, so the settings reach it through the game's
// per-object field overrides (ZNetView.LoadFields reads "HasFields", "HasFields<Component>" and
// "<Component>.<field>" from the ZDO). Clients read them only when they load the creature, so a
// creature whose overrides change is replaced with an identical copy, the way ServersideQoL does it.
[BepInPlugin(Guid, "BreedingInPen", Version)]
public class BreedingInPenPlugin : BaseUnityPlugin
{
	public const string Guid = "Tie.BreedingInPen";

	public const string Version = "1.0.1";

	private static readonly int s_hasFields = "HasFields".GetStableHashCode();

	private static readonly int s_hasProcreationFields = "HasFieldsProcreation".GetStableHashCode();

	private static readonly int s_maxCreaturesField = "Procreation.m_maxCreatures".GetStableHashCode();

	private static readonly int s_spawnOffsetField = "Procreation.m_spawnOffset".GetStableHashCode();

	private static readonly int s_spawnOffsetMaxField = "Procreation.m_spawnOffsetMax".GetStableHashCode();

	private static readonly int KeyPlaced = "BreedingInPen_placed".GetStableHashCode();

	private ConfigEntry<float> _tickSeconds;

	private ConfigEntry<bool> _debugLog;

	private ConfigEntry<bool> _birthAtParent;

	private ConfigEntry<bool> _birthBetweenParents;

	private ConfigEntry<float> _newbornSeconds;

	private readonly Dictionary<int, Breeder> _breeders = new Dictionary<int, Breeder>();

	// Baby prefab (Boar_piggy, Wolf_cub, Lox_Calf, ...) to the prefabs that give birth to it.
	private readonly Dictionary<int, List<int>> _parentsOf = new Dictionary<int, List<int>>();

	private readonly List<ZDO> _sectorObjects = new List<ZDO>();

	private readonly HashSet<ZDOID> _seen = new HashSet<ZDOID>();

	private readonly List<ZDO> _adults = new List<ZDO>();

	private readonly List<ZDO> _young = new List<ZDO>();

	private bool _resolved;

	private float _timer;

	private sealed class Breeder
	{
		public string Name;

		public ConfigEntry<int> MaxCreatures;

		public int Default;

		public float SpawnOffset;

		public float SpawnOffsetMax;

		public float PartnerRange;
	}

	private void Awake()
	{
		_tickSeconds = Config.Bind("General", "TickSeconds", 2f, "How often (seconds) creatures near players are checked. Newborns are moved on the first check after birth.");
		_debugLog = Config.Bind("General", "DebugLog", false, "Log every creature changed or newborn moved.");
		_birthAtParent = Config.Bind("Pen", "BirthAtParent", true, "Babies appear where the mother stands instead of up to 2 m behind her, so they never start on the other side of a pen fence.");
		_birthBetweenParents = Config.Bind("Pen", "BirthBetweenParents", true, "A newborn is moved between its mother and her nearest tamed partner a moment after birth (it may visibly jump once). Both parents are inside the pen, so the baby is too.");
		_newbornSeconds = Config.Bind("Pen", "NewbornSeconds", 20f, "Only tamed babies younger than this are moved, so babies that already walked around are left alone.");
	}

	private void Update()
	{
		if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || ZNetScene.instance == null)
		{
			_resolved = false;
			return;
		}
		_timer += Time.deltaTime;
		if (_timer < Mathf.Max(0.5f, _tickSeconds.Value))
		{
			return;
		}
		_timer = 0f;
		try
		{
			if (!_resolved)
			{
				Resolve();
			}
			Tick();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	// One MaxCreatures setting per creature the game breeds, in [MaxCreatures]; -1 keeps the game's value.
	private void Resolve()
	{
		_resolved = true;
		_breeders.Clear();
		_parentsOf.Clear();
		foreach (GameObject go in ZNetScene.instance.m_prefabs.OrderBy(g => g.name, StringComparer.Ordinal))
		{
			Procreation procreation = go.GetComponent<Procreation>();
			if (procreation == null || go.GetComponent<Character>() == null || go.GetComponent<Tameable>() == null || go.GetComponent<ZNetView>() == null)
			{
				continue;
			}
			_breeders[go.name.GetStableHashCode()] = new Breeder
			{
				Name = go.name,
				MaxCreatures = Config.Bind("MaxCreatures", go.name, -1, $"Tamed {go.name} stop breeding when this many {go.name} and their young are within {procreation.m_totalCheckRange:0.#} m (the game's check). -1 keeps the game's value, {procreation.m_maxCreatures}."),
				Default = procreation.m_maxCreatures,
				SpawnOffset = procreation.m_spawnOffset,
				SpawnOffsetMax = procreation.m_spawnOffsetMax,
				PartnerRange = procreation.m_partnerCheckRange
			};
			GameObject baby = procreation.m_offspring == null ? null : ZNetScene.instance.GetPrefab(procreation.m_offspring.name);
			if (baby != null && baby != go && baby.GetComponent<Character>() != null)
			{
				int hash = baby.name.GetStableHashCode();
				if (!_parentsOf.TryGetValue(hash, out List<int> parents))
				{
					_parentsOf[hash] = parents = new List<int>();
				}
				parents.Add(go.name.GetStableHashCode());
			}
		}
		string limits = string.Join(", ", _breeders.Values.Where(b => b.MaxCreatures.Value >= 0).Select(b => b.Name + " " + b.MaxCreatures.Value).DefaultIfEmpty("game values"));
		Logger.LogInfo($"{_breeders.Count} breeding creature(s): {string.Join(", ", _breeders.Values.Select(b => b.Name))}. Limits: {limits}. Birth at parent: {_birthAtParent.Value}, between parents: {_birthBetweenParents.Value}.");
	}

	private void Tick()
	{
		if (_breeders.Count == 0)
		{
			return;
		}
		Collect();
		foreach (ZDO zdo in _adults)
		{
			ApplyOverrides(zdo);
		}
		if (_birthBetweenParents.Value)
		{
			foreach (ZDO zdo in _young)
			{
				PlaceNewborn(zdo);
			}
		}
	}

	// Creatures in the areas simulated around connected players; breeding only runs there.
	private void Collect()
	{
		_adults.Clear();
		_young.Clear();
		_seen.Clear();
		SimulationDistance synced = ZNet.instance.GetSyncedSimulationDistance();
		SimulationDistance near = new SimulationDistance(synced.NearSimulationDistance, 0, synced.IsClassic);
		List<Vector3> centers = ZNet.instance.GetPeers().Select(p => p.GetRefPos()).ToList();
		if (!ZNet.instance.IsDedicated())
		{
			centers.Add(ZNet.instance.GetReferencePosition());
		}
		foreach (Vector3 center in centers)
		{
			_sectorObjects.Clear();
			ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(center), near, _sectorObjects);
			foreach (ZDO zdo in _sectorObjects)
			{
				if (!zdo.IsValid() || !_seen.Add(zdo.m_uid) || !zdo.GetBool(ZDOVars.s_tamed))
				{
					continue;
				}
				int prefab = zdo.GetPrefab();
				if (_breeders.ContainsKey(prefab))
				{
					_adults.Add(zdo);
				}
				else if (_parentsOf.ContainsKey(prefab))
				{
					_young.Add(zdo);
				}
			}
		}
	}

	private void ApplyOverrides(ZDO zdo)
	{
		Breeder b = _breeders[zdo.GetPrefab()];
		bool atParent = _birthAtParent.Value;
		bool changed = Override(zdo, s_maxCreaturesField, b.MaxCreatures.Value >= 0 ? b.MaxCreatures.Value : null, b.Default);
		changed |= Override(zdo, s_spawnOffsetField, atParent ? 0f : null, b.SpawnOffset);
		changed |= Override(zdo, s_spawnOffsetMaxField, atParent ? 0f : null, b.SpawnOffsetMax);
		if (!changed)
		{
			return;
		}
		zdo.Set(s_hasFields, true);
		zdo.Set(s_hasProcreationFields, true);
		ZDO copy = Replace(zdo, zdo.GetPosition());
		Debug($"{b.Name} {zdo.m_uid}: breeding limit {zdo.GetInt(s_maxCreaturesField, b.Default)}, birth at parent {atParent}; replaced by {copy.m_uid}.");
	}

	// Sets a field override to the wanted value; with no wanted value, an existing override goes back to
	// the game's value. Returns whether the ZDO changed.
	private static bool Override(ZDO zdo, int key, int? wanted, int gameValue)
	{
		bool has = zdo.GetInt(key, out int current);
		int value = wanted ?? gameValue;
		if (wanted == null && !has || has && current == value)
		{
			return false;
		}
		zdo.Set(key, value);
		return true;
	}

	private static bool Override(ZDO zdo, int key, float? wanted, float gameValue)
	{
		bool has = zdo.GetFloat(key, out float current);
		float value = wanted ?? gameValue;
		if (wanted == null && !has || has && current == value)
		{
			return false;
		}
		zdo.Set(key, value);
		return true;
	}

	// The game spawns a baby at (or just behind) its mother. A tamed baby a few seconds old is moved
	// between the mother and her nearest tamed partner.
	private void PlaceNewborn(ZDO baby)
	{
		long born = baby.GetLong(ZDOVars.s_spawnTime, 0L);
		if (born == 0L || !baby.GetBool(ZDOVars.s_tamed) || baby.GetBool(KeyPlaced) || (ZNet.instance.GetTime() - new DateTime(born)).TotalSeconds > _newbornSeconds.Value)
		{
			return;
		}
		Vector3 pos = baby.GetPosition();
		List<int> parents = _parentsOf[baby.GetPrefab()];
		ZDO mother = _adults.Where(z => parents.Contains(z.GetPrefab()) && z.GetBool(ZDOVars.s_tamed) && (z.GetPosition() - pos).sqrMagnitude <= 16f)
			.OrderBy(z => (z.GetPosition() - pos).sqrMagnitude).FirstOrDefault();
		if (mother == null)
		{
			return;
		}
		Vector3 at = mother.GetPosition();
		float range = Mathf.Max(_breeders[mother.GetPrefab()].PartnerRange, 3f) + 5f;
		ZDO partner = _adults.Where(z => z != mother && z.GetPrefab() == mother.GetPrefab() && z.GetBool(ZDOVars.s_tamed) && (z.GetPosition() - at).sqrMagnitude <= range * range)
			.OrderBy(z => (z.GetPosition() - at).sqrMagnitude).FirstOrDefault();
		baby.Set(KeyPlaced, true);
		if (partner == null)
		{
			return;
		}
		Vector3 target = Vector3.Lerp(at, partner.GetPosition(), UnityEngine.Random.Range(0.3f, 0.7f));
		target.y = Mathf.Max(at.y, partner.GetPosition().y) + 0.5f;
		ZDO copy = Replace(baby, target);
		Debug($"{ZNetScene.instance.GetPrefab(baby.GetPrefab()).name} {baby.m_uid} born at ({pos.x:0}, {pos.y:0}, {pos.z:0}) moved between its parents to ({target.x:0}, {target.y:0}, {target.z:0}) as {copy.m_uid}.");
	}

	// Replaces a ZDO with an identical copy at the given position. The players' games drop the old object
	// and load the copy, which is how a creature a client controls can be changed or moved by the server.
	private static ZDO Replace(ZDO zdo, Vector3 pos)
	{
		ZPackage pkg = new ZPackage();
		zdo.Serialize(pkg);
		pkg.SetPos(0);
		ZDO copy = ZDOMan.instance.CreateNewZDO(pos, zdo.GetPrefab());
		copy.Deserialize(pkg);
		copy.SetPosition(pos);
		copy.SetOwner(0L);
		zdo.SetOwnerInternal(ZDOMan.GetSessionID());
		ZDOMan.instance.DestroyZDO(zdo);
		return copy;
	}

	private void Debug(string text)
	{
		if (_debugLog.Value)
		{
			Logger.LogInfo(text);
		}
	}
}
