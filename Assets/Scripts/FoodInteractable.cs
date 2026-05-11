using UnityEngine;
using System.Collections;

public class FoodInteractable : MonoBehaviour
{
    public FoodData foodData;

    private bool playerInside;

    private void Update()
    {
        if (!GameManager.Instance.IsExploration())
            return;

        if (playerInside && Input.GetKeyDown(KeyCode.E))
        {
            Eat();
        }
    }

    void Eat()
    {
        // Buscar energia del jugador
        PlayerEnergy playerEnergy = FindObjectOfType<PlayerEnergy>();

        if (playerEnergy != null)
        {
            playerEnergy.AddEnergy(foodData.energyValue);
        }

        // Buscar animator del billboard
        Alpha_2D_Character_In_3D_World player =
            FindObjectOfType<Alpha_2D_Character_In_3D_World>();

        if (player != null)
        {
            Animator anim =
                player.billboard.GetComponent<Animator>();

            if (anim != null)
            {
                anim.SetTrigger("EatTrigger");
            }
        }

        StartCoroutine(DestroyFood());
    }

    IEnumerator DestroyFood()
    {
        yield return new WaitForSeconds(1f);

        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
        }
    }
}
