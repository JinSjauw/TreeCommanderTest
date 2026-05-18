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
    private EnemyController enemyController;
    private DeathHandler deathHandler;

    private void Awake()
    {
        health = maxHealth;

        deathHandler = GetComponent<DeathHandler>();
    }

    private void OnEnable()
    {
        health = maxHealth;

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
        //Disable Vehicle
        if (!isPlayer) 
        {
            enemyController.Die();
        }

        deathHandler.SpawnCorpse();

        //Debug.Log(gameObject.name + " Has been Destroyed");
    }
}
