using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public sealed class GameplayObjectPool : MonoBehaviour
{
    private const int k_EnemyPrewarmCount = 36;
    private const int k_EnemyMaxInactive = 40;
    private const int k_ProjectileDefaultCapacity = 16;
    private const int k_ProjectileMaxInactive = 128;
    private const int k_MaxActiveAmmo = 30;

    [SerializeField] private EnemyHealth m_suicideEnemyPrefab;
    [SerializeField] private EnemyHealth m_meleeEnemyPrefab;
    [SerializeField] private EnemyHealth m_rangedEnemyPrefab;
    [SerializeField] private RangedProjectile m_projectilePrefab;
    [SerializeField] private RangedProjectileGasEmitter m_projectileGasEmitter;
    [SerializeField] private AmmoPickup m_ammoPickupPrefab;

    private readonly LinkedList<AmmoPickup> m_activeAmmo = new();
    private ObjectPool<EnemyHealth>[] m_enemyPools;
    private ObjectPool<RangedProjectile> m_projectilePool;
    private ObjectPool<AmmoPickup> m_ammoPool;

    public int ActiveEnemyCount { get; private set; }
    public bool IsConfigured => m_suicideEnemyPrefab != null && m_meleeEnemyPrefab != null
        && m_rangedEnemyPrefab != null && m_projectilePrefab != null
        && m_projectileGasEmitter != null && m_ammoPickupPrefab != null;

    private void Awake()
    {
        m_enemyPools = new[]
        {
            CreateEnemyPool(m_suicideEnemyPrefab),
            CreateEnemyPool(m_meleeEnemyPrefab),
            CreateEnemyPool(m_rangedEnemyPrefab)
        };
        m_projectilePool = new ObjectPool<RangedProjectile>(CreateProjectile, null, ReleaseProjectileObject,
            DestroyProjectile, true, k_ProjectileDefaultCapacity, k_ProjectileMaxInactive);
        m_ammoPool = new ObjectPool<AmmoPickup>(CreateAmmo, null, ReleaseAmmoObject,
            DestroyAmmo, true, k_MaxActiveAmmo, k_MaxActiveAmmo);
        for (int i = 0; i < m_enemyPools.Length; i++)
        {
            Prewarm(m_enemyPools[i], k_EnemyPrewarmCount);
        }
        Prewarm(m_projectilePool, k_ProjectileDefaultCapacity);
        SpatialAudio.Initialize(transform);
    }

    public EnemyHealth SpawnEnemy(int type, Vector3 position, Quaternion rotation)
    {
        int index = Mathf.Clamp(type, 0, m_enemyPools.Length - 1);
        EnemyHealth enemy = m_enemyPools[index].Get();
        enemy.PrepareForSpawn(position, rotation, this);
        enemy.gameObject.SetActive(true);
        ActiveEnemyCount++;
        return enemy;
    }

    public void ReleaseEnemy(EnemyHealth enemy)
    {
        if (enemy == null || enemy.IsPooled)
        {
            return;
        }

        enemy.MarkPooled();
        ActiveEnemyCount = Mathf.Max(0, ActiveEnemyCount - 1);
        m_enemyPools[(int)enemy.Type].Release(enemy);
    }

    public RangedProjectile SpawnProjectile(Vector3 position, Vector3 direction, float speed,
        float damage, float radius, float lifetime)
    {
        RangedProjectile projectile = m_projectilePool.Get();
        projectile.gameObject.SetActive(true);
        projectile.Launch(this, position, direction, speed, damage, radius, lifetime);
        return projectile;
    }

    public void ReleaseProjectile(RangedProjectile projectile)
    {
        if (projectile == null || projectile.IsPooled)
        {
            return;
        }

        projectile.MarkPooled();
        m_projectilePool.Release(projectile);
    }

    public void EmitProjectileCharge(Vector3 position)
    {
        m_projectileGasEmitter?.EmitChargeAt(position);
    }

    public void EmitProjectileTrail(Vector3 position)
    {
        m_projectileGasEmitter?.EmitTrailAt(position);
    }

    public void EmitProjectileImpact(Vector3 position)
    {
        m_projectileGasEmitter?.EmitImpactAt(position);
    }

    public AmmoPickup SpawnAmmo(Vector3 deathPosition, WeaponId weapon, int amount)
    {
        if (m_activeAmmo.Count == k_MaxActiveAmmo)
        {
            ReleaseAmmo(m_activeAmmo.First.Value);
        }

        AmmoPickup pickup = m_ammoPool.Get();
        pickup.PrepareForSpawn(this, AmmoPickup.FindGroundPosition(deathPosition), weapon, amount);
        pickup.gameObject.SetActive(true);
        m_activeAmmo.AddLast(pickup);
        return pickup;
    }

    public AmmoPickup SpawnAmmoDrop(Vector3 deathPosition)
    {
        FirstPersonController player = FirstPersonController.CurrentInstance;
        WeaponId primary = player != null ? player.PrimaryWeapon : WeaponId.Rifle;
        WeaponId weapon = AmmoPickup.ChooseAmmoWeapon(primary, Random.value);
        return SpawnAmmo(deathPosition, weapon, AmmoPickup.GetAmount(weapon));
    }

    public void ReleaseAmmo(AmmoPickup pickup)
    {
        if (pickup == null || pickup.IsPooled)
        {
            return;
        }

        // ponytail: at most 30 entries, so a linked-list scan beats another lookup table.
        m_activeAmmo.Remove(pickup);
        pickup.MarkPooled();
        m_ammoPool.Release(pickup);
    }

    private ObjectPool<EnemyHealth> CreateEnemyPool(EnemyHealth prefab)
    {
        return new ObjectPool<EnemyHealth>(() =>
        {
            EnemyHealth enemy = Instantiate(prefab, transform);
            enemy.gameObject.SetActive(false);
            return enemy;
        }, null, enemy => enemy.gameObject.SetActive(false), enemy => Destroy(enemy.gameObject),
            true, k_EnemyPrewarmCount, k_EnemyMaxInactive);
    }

    private RangedProjectile CreateProjectile()
    {
        RangedProjectile projectile = Instantiate(m_projectilePrefab, transform);
        projectile.gameObject.SetActive(false);
        return projectile;
    }

    private static void ReleaseProjectileObject(RangedProjectile projectile)
    {
        projectile.gameObject.SetActive(false);
    }

    private static void DestroyProjectile(RangedProjectile projectile)
    {
        Destroy(projectile.gameObject);
    }

    private AmmoPickup CreateAmmo()
    {
        AmmoPickup pickup = Instantiate(m_ammoPickupPrefab, transform);
        pickup.gameObject.SetActive(false);
        return pickup;
    }

    private static void ReleaseAmmoObject(AmmoPickup pickup)
    {
        pickup.gameObject.SetActive(false);
    }

    private static void DestroyAmmo(AmmoPickup pickup)
    {
        Destroy(pickup.gameObject);
    }

    private static void Prewarm<T>(ObjectPool<T> pool, int count) where T : Component
    {
        var instances = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            instances.Add(pool.Get());
        }

        for (int i = 0; i < instances.Count; i++)
        {
            pool.Release(instances[i]);
        }
    }

    [ContextMenu("Run Gameplay Object Pool Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(IsConfigured);
        Debug.Assert(ActiveEnemyCount >= 0);
        Debug.Assert(m_activeAmmo.Count <= k_MaxActiveAmmo);
        Debug.Assert(m_enemyPools != null && m_enemyPools.Length == 3);
        for (int index = 0; index < m_enemyPools.Length; index++)
        {
            Debug.Assert(m_enemyPools[index].CountAll >= k_EnemyPrewarmCount);
            Debug.Assert(m_enemyPools[index].CountInactive >= k_EnemyPrewarmCount
                || ActiveEnemyCount > 0);
        }
        Debug.Assert(m_projectileGasEmitter != null);
    }
}
