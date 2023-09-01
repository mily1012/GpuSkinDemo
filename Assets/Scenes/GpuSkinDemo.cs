using System;
using UnityEngine;
using System.Collections.Generic;
using System.Resources;
using UnityEditor;

public class GpuSkinDemo : MonoBehaviour
{
    public string m_gpuModelPath;
    public Transform m_objRoot;

    private List<GPUSkinAnimator> m_gpuAnimators = new List<GPUSkinAnimator>();

    int m_row = 20;
    int m_col = 10;
    int m_spaceZ = 3;
    int m_spaceX = 3;
    void Awake()
    {

    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(50);
        GUILayout.BeginVertical();
        GUILayout.Space(50);
        if (GUILayout.Button("CreateGpuPrefab",GUILayout.Width(150),GUILayout.Height(48)))
        {
            CreateGpuPrefab();
        }

        GUILayout.Space(10);

        GUILayout.Space(10);

        if (GUILayout.Button("单位数量200", GUILayout.Width(150), GUILayout.Height(48)))
        {
            SetRowColNumber(20,10);
        }

        GUILayout.Space(10);

        if (GUILayout.Button("单位数量400", GUILayout.Width(150), GUILayout.Height(48)))
        {
            SetRowColNumber(20,20);
        }

        GUILayout.Space(10);

        if (GUILayout.Button("单位数量600", GUILayout.Width(150), GUILayout.Height(48)))
        {
            SetRowColNumber(30,20);
        }

        if (GUILayout.Button("单位数量900", GUILayout.Width(150), GUILayout.Height(48)))
        {
            SetRowColNumber(30,30);
        }

        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.BeginVertical();
        GUILayout.Space(50);
        if (GUILayout.Button("GpuIdle", GUILayout.Width(120), GUILayout.Height(48)))
        {
            PlayAnim("Idle");
        }
        GUILayout.Space(10);

        if (GUILayout.Button("GpuRun", GUILayout.Width(120), GUILayout.Height(48)))
        {
            PlayAnim("Run");
        }

        if (GUILayout.Button("GpuTurn_Left",GUILayout.Width(120), GUILayout.Height(48)))
        {
            PlayAnim("Turn_Left");
        }
        GUILayout.Space(10);

        if (GUILayout.Button("GpuTurn_Right",GUILayout.Width(120), GUILayout.Height(48)))
        {
            PlayAnim("Turn_Right");
        }
        GUILayout.Space(10);

        if (GUILayout.Button("GpuAttackStart", GUILayout.Width(120), GUILayout.Height(48)))
        {
            PlayAnim("AttackStart");
        }
        GUILayout.Space(10);

        if (GUILayout.Button("GpuAttackLoop", GUILayout.Width(120), GUILayout.Height(48)))
        {
            PlayAnim("AttackLoop");
        }
        GUILayout.Space(10);

        if (GUILayout.Button("GpuAttackEnd", GUILayout.Width(120), GUILayout.Height(48)))
        {
            PlayAnim("AttackEnd");
        }
        GUILayout.Space(10);

        if (GUILayout.Button("GpuSpell1_1", GUILayout.Width(120), GUILayout.Height(48)))
        {
            PlayAnim("Spell1_1");
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private void SetRowColNumber(int row,int col)
    {
        m_row = row;
        m_col = col;
    }

    private void CreateGpuPrefab()
    {
        ClearPrafab();

        for( int i = 0; i < m_row; i++)
        {
            int posZ = (i - m_row / 2) * m_spaceZ;
            for( int k =0; k < m_col; k++)
            {
                int posX = (k - m_col / 2) * m_spaceX;
                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<GameObject>(m_gpuModelPath);
                GameObject go = Instantiate(obj) as GameObject;
                go.transform.SetParent(m_objRoot);
                go.transform.localPosition = new Vector3(posX, 0, posZ);

                GPUSkinAnimator animator = go.GetComponent<GPUSkinAnimator>();
                m_gpuAnimators.Add(animator);

                animator.Play("Idle");
            }
        }
    }

    private void ClearPrafab()
    {
        for( int i= 0; i < m_objRoot.childCount;i++)
        {
            Destroy(m_objRoot.GetChild(i).gameObject);
            m_gpuAnimators.Clear();
        }
    }

    private void PlayAnim(string anim)
    {
        for( int i= 0; i < m_gpuAnimators.Count;i++)
        {
            m_gpuAnimators[i].CrossFade(anim,0.2f);
        }
    }
}
