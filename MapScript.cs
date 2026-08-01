namespace Realm.Maps;

using System.Numerics;
using Realm.MapAPI;

public class CustomMap : IWasmModule
{
    private int _live = 20;
    private int _currentWave = 0;
    private const int _maxWaves = 3;
    private bool _waveInProgress = false;

    private float _waveTimer = 0.5f;
    private int _monsterToSpawn = 0;
    private float _spawnTimer = 0f;
    private float _spawnInterval = 1.2f;

    private int _goalZoneHandle;
    private Vector3 _spawnPos;
    private Vector3 _goalPos;

    private IUnit? _playerHero;

    private readonly string[] _groundMonsters = ["zombie_soldier"];
    private readonly string _flyingMonster = "cyber_dragon";
    private readonly string _bossMonster = "black_armored_robot";
    private readonly string _heroModel = "adventurer";


    public void Initialize(IGameAPI api)
    {
        api.Gold = 300;
        api.SetLeaderboardVisible("Tower Defense", true);
        api.AddLeaderboardRow("Vidas", _live.ToString(), new Vector3(0, 1, 0));
        api.AddLeaderboardRow("Oleada", $"{_currentWave} / {_maxWaves}", new Vector3(1, 1, 0));

        _spawnPos = (Coordinates.spawnzone.Min + Coordinates.spawnzone.Max) * 0.5f;
        _goalPos = (Coordinates.goal.Min + Coordinates.goal.Max) * 0.5f;

        _playerHero = api.SpawnUnitForPlayer(_heroModel, _goalPos + new Vector3(-5f, 0f, -5f), playerIndex: 0);
        api.ShowFeedbackText("¡Héroe desplegado!", new Vector3(0, 1, 0));

        _goalZoneHandle = api.DefineZone(
            Coordinates.goal.Min.X, Coordinates.goal.Min.Z,
            Coordinates.goal.Max.X, Coordinates.goal.Max.Z
        );

        api.OnUnitEnterZone += (unit, zoneHandle) =>
        {
            if (zoneHandle == _goalZoneHandle && unit.IsEnemy)
            {
                _live--;
                api.SetLeaderboardValue("Vidas", _live.ToString());
                api.DestroyUnit(unit);
                api.ShowFeedbackText("-1 Vida!", new Vector3(1, 0, 0));
                api.PlayWarningSound();
                if (_live <= 0)
                {
                    api.TriggerDefeat();
                }
            }
        };

        api.OnUnitDied += (victim, killer) =>
       {
           if (victim.IsEnemy)
           {
               api.Gold += 15;
               api.CreateFloatingText("+15 Or", victim.Position, new Vector3(1, 0.85f, 0), 1.2f);
           }
       };
        api.BroadcastMessage("¡La partida ha comenzado! Prepárate para defender el reino.");


    }

    public void Update(IGameAPI api, float delta)
    {
        if (_live <= 0) return;

        if (!_waveInProgress)
        {
            _waveTimer -= delta;
            if (_waveTimer <= 0f)
            {
                _currentWave++;
                if (_currentWave > _maxWaves)
                {
                    api.BroadcastMessage("¡Felicidades! Has defendido el reino exitosamente.");
                    api.TriggerVictory();
                    return;
                }
                _waveInProgress = true;
                _monsterToSpawn = GetMonsterCountForWave(_currentWave);
                api.SetLeaderboardValue("Oleada", $"{_currentWave} / {_maxWaves}");
                api.BroadcastMessage($"¡Oleada {_currentWave} iniciada!");
            }
            return;
        }

        if (_monsterToSpawn > 0)
        {
            _spawnTimer += delta;
            if (_spawnTimer >= _spawnInterval)
            {
                _spawnTimer = 0f;
                _monsterToSpawn--;
                string monsterType = SelectMonsterForWave(_currentWave, _monsterToSpawn);
                IUnit monster = api.SpawnUnit(monsterType, _spawnPos, isEnemy: true);
                api.IssueAttackMoveOrder(monster, _goalPos);
            }
        }
        else
        {
            int activeEnemies = 0;
            foreach (var unit in api.GetAllUnits())
            {
                if (unit.IsEnemy) activeEnemies++;
            }
            if (activeEnemies == 0)
            {
                _waveInProgress = false;
                _waveTimer = 10.0f;
                api.BroadcastMessage($"Oleada {_currentWave} superada. Próxima oleada en 10 segundos.");
            }
        }
    }

    private int GetMonsterCountForWave(int wave)
    {
        return wave switch
        {
            1 => 10,
            2 => 15,
            3 => 20,
            _ => 10
        };
    }
    private string SelectMonsterForWave(int wave, int countLeft)
    {
        if (wave == 1) return _groundMonsters[0];
        if (wave == 2) return (countLeft % 3 == 0) ? _flyingMonster : _groundMonsters[0];

        // Oleada 3: El último monstruo en nacer es el Jefe/Boss
        if (countLeft == 0) return _bossMonster;
        return (countLeft % 2 == 0) ? _flyingMonster : _groundMonsters[0];
    }
}
