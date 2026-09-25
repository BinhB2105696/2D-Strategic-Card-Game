using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum CardElement
{
    Metal,
    Wood,
    Fire,
    Water,
    Earth,
    Wind
}
public class CardData
{
    // Variable (Biến) lưu thông tin lá bài
    public string cardName;     // Kiểu chữ (Vùng 1)
    public int attack;          // Kiểu số nguyên (Vùng 3)
    public int defense;         // Vùng 4
    public int health;          // Vùng 5
    public CardElement element;

    public void TakeDamage(int damageAmount)
    {
        health -= damageAmount; // Giảm máu dựa trên sát thương nhận vào
    }

    public bool IsDead
    {
        get { return health <= 0; } // Chỉ cho phép đọc (get), không cho sửa trực tiếp từ ngoài
    }

}
