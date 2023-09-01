using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPUSkinningPlayerMonoManager
{
    private List<GPUSkinningPlayerResources> playerResourcesList = new List<GPUSkinningPlayerResources>();

    public void Register(GPUSkinningAnimation anim, Mesh mesh, Material[] originalMtrl, Texture2D animTexture, GPUSkinAnimator player, out GPUSkinningPlayerResources playerResources)
    {
        playerResources = null;

        if (anim == null || mesh == null || originalMtrl == null || animTexture == null || player == null)
        {
            return;
        }

        GPUSkinningPlayerResources item = null;

        foreach(var playerRes in playerResourcesList)
        {
            if(playerRes.GUID == anim.guid)
            {
                item = playerRes;
                break;
            }
        }

        if(item == null)
        {
            item = new GPUSkinningPlayerResources();
            playerResourcesList.Add(item);
        }

        item.Init(mesh, originalMtrl, animTexture, anim);

        if (!item.players.Contains(player))
        {
            item.players.Add(player);
        }

        playerResources = item;
    }

    public void Unregister(GPUSkinAnimator player)
    {
        if(player == null)
            return;

        int numItems = playerResourcesList.Count;
        for(int i = 0; i < numItems; ++i)
        {
            int playerIndex = playerResourcesList[i].players.IndexOf(player);
            if(playerIndex != -1)
            {
                playerResourcesList[i].players.RemoveAt(playerIndex);
                if(playerResourcesList[i].players.Count == 0)
                {
                    playerResourcesList[i].Destroy();
                    playerResourcesList.RemoveAt(i);
                }
                break;
            }
        }
    }
}
