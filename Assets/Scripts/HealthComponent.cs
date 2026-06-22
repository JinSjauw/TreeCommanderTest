using System;
using UnityEngine;

[RequireComponent(typeof(DeathHandler))]
public class HealthComponent : MonoBehaviour
{
    //[SerializeField] private UIEvents uiEvents;
    [SerializeField] private float maxHealth;
    [SerializeField] private float health;
    //[SerializeField] private float percentageHealth;

    private bool isPlayer;
    private bool hasDied = false;
    private EnemyController enemyController;
    private DeathHandler deathHandler;

    public bool HasDied => hasDied;

    private void Awake()
    {
        health = maxHealth;

        deathHandler = GetComponent<DeathHandler>();
    }

    private void OnEnable()
    {
        health = maxHealth;
        hasDied = false;

        EnemyController enemy = GetComponentInParent<EnemyController>();
        if (enemy)
        {
            isPlayer = false;
            enemyController = enemy;
        }
        else
        {
            isPlayer = true;
            //uiEvents.OnHealthChanged(health);

            //uiEvents.newWaveEvent += OnNewWave;
        }
    }

    private void OnDisable()
    {
        if (isPlayer) 
        {
            //uiEvents.newWaveEvent -= OnNewWave;
        }
    }

    private void OnNewWave(int waveNumber, int enemiesAmount)
    {
        health = maxHealth + enemiesAmount * 5;
        //uiEvents.OnHealthChanged(health);
    }

    public void TakeDamage(float damage) 
    {
        health -= damage;
        //Caculate percentage HP
        if (isPlayer) 
        {
            //uiEvents.OnHealthChanged(health);
        }
        
        if (health <= 0) 
        {
            Die();
        }
    }

    private void Die() 
    {
        Debug.Log($"{gameObject.name} Has been died! hasDied? {hasDied}");
        if(hasDied) return;
        //Disable Vehicle
        if (!isPlayer) 
        {
            enemyController.Die();
        }

        deathHandler.SpawnCorpse();
        hasDied = true;
    }
}
