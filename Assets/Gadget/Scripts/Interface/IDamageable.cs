using UnityEngine;

public interface IDamageable
{
    void TakeDamage(int amount);
}

/*
public class PlayerHealth : MonoBehaviour, IDamageable
{
    public int hp = 100;

    public void TakeDamage(int amount)
    {
        hp -= amount;
        Debug.Log($"플레이어 데미지: {amount}, 남은 HP: {hp}");
    }
}
 */