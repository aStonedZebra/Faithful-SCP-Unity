using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SCP_106 : Roam_NPC
{
    NavMeshAgent _navMeshagent;
    public LayerMask Ground;
    float PlayerDistance= 20, timer, ambianceTimer;
    public GameObject Player;
    bool isActive = false, playedHorror, usingAStar = true, isSpawn = false, isBlocked = false, isOut = false, isPath;
    Quaternion toAngle, realAngle;
    public float speed, spawntimer, Distance;
    float escapeTimer;
    bool Escaped=false, lastDest, isChase=false;
    AudioSource sfx;
    Vector3 Destination;
    int frameInterval=20;
    public AudioClip[] Horror, Sfx;
    public AudioClip music;
    public Animator anim;
    NavMeshHit shit;
    Vector3 velocity;
    Transform[] ActualPath;
    int currentNode;
    public CapsuleCollider col;



    void Start()
    {
        Player = GameController.instance.player;
        _navMeshagent = this.GetComponent<NavMeshAgent>();
        sfx = GetComponent<AudioSource>();
        _navMeshagent.enabled = false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawSphere(transform.position, 0.3f);
        Gizmos.DrawSphere(transform.position + (Vector3.up * 0.5f), 0.3f);
    }

    void Update()
    {
        if (isActive)
        {
            if (!isEvent)
            {
                if (Time.frameCount % 10 == 0)
                {
                    PlayerDistance = (Vector3.Distance(new Vector3(Player.transform.position.x, transform.position.y, Player.transform.position.z), transform.position));
                    isOut = !NavMesh.SamplePosition(transform.position, out shit, 0.2f, NavMesh.AllAreas);

                    isBlocked = Physics.CheckSphere(transform.position + (Vector3.up * 0.5f), 0.3f, Ground, QueryTriggerInteraction.Ignore);

                }


                timer -= Time.deltaTime;
                if (timer <= 0 && isSpawn == false)
                {
                    isSpawn = true;
                    col.enabled = true;
                }

                escapeTimer += Time.deltaTime;
                Debug.Log(escapeTimer);

                if (agroLevel == 0 && escapeTimer >= 30)
                {
                    Escaped = true;
                }
                if (agroLevel == 1 && escapeTimer >= 45)
                {
                    Escaped = true;
                }


                if (PlayerDistance < 3f || isBlocked || isOut)
                {
                    usingAStar = false;
                }
                else
                    usingAStar = true;

                DoSFX();


                if (isSpawn)
                {
                    HorrorPlay();
                    if (usingAStar)
                    {
                        _navMeshagent.enabled = true;
                        if (Time.frameCount % frameInterval == 0)
                            SetDestination();

                    }
                    else
                    {
                        Vector3 Point = new Vector3(Player.transform.position.x, transform.position.y, Player.transform.position.z) - transform.position;
                        toAngle = Quaternion.LookRotation(Point);
                        realAngle = Quaternion.LookRotation(new Vector3(Player.transform.position.x, Player.transform.position.y - 0.4f, Player.transform.position.z) - transform.position);
                        _navMeshagent.enabled = false;

                        transform.position += (realAngle * (Vector3.forward * speed)) * Time.deltaTime;
                        transform.rotation = Quaternion.Lerp(transform.rotation, toAngle, 1f * Time.deltaTime);
                    }

                    if (agroLevel != 0 && PlayerDistance > 20 && !Escaped)
                    {
                        Spawn(true, new Vector3(Player.transform.position.x, 0.01f, Player.transform.position.z));
                    }

                    if (Escaped && PlayerDistance > 8 && _navMeshagent.remainingDistance < _navMeshagent.radius)
                        UnSpawn();


                }

                bool shouldMove = velocity.magnitude > 0.5f;

                // Update animation parameters
                anim.SetBool("move", isSpawn);

            }
            else
            {
                if (isPath)
                    Path();

                anim.SetBool("move", isPath);
            }
        }
    }


    void DoSFX()
    {

        ambianceTimer -= Time.deltaTime;
        if (ambianceTimer <= 0)
        {
            sfx.PlayOneShot(Sfx[Random.Range(1, Sfx.Length)]);
            ambianceTimer = 2 * Random.Range(1, 5);
        }
    }

    void HorrorPlay()
    {
        if (PlayerDistance < 16 && PlayerDistance > 4)
        {
                if (playedHorror == false)
                {
                    GameController.instance.PlayHorror(Horror[Random.Range(0, Horror.Length)], this.transform, npc.scp106);
                    playedHorror = true;
                }
        }
    }

    public override void UnSpawn()
    {
        _navMeshagent.enabled = false;
        transform.position = (new Vector3(0, -10, 0));
        isActive = false;
        isSpawn = false;
        isChase = false;
        Escaped = false;

        GameController.instance.DefMusic();
    }

    public override void Spawn(bool beActive, Vector3 here)
    {
        col.enabled = false;
        anim.SetBool("move", false);
        anim.SetTrigger("spawn");
        transform.position = here;
        _navMeshagent.enabled = true;
        _navMeshagent.Warp(here);
        isActive = true;
        isSpawn = false;
        sfx.PlayOneShot(Sfx[0]);
        
        playedHorror = false;
        DecalSystem.instance.Decal(here, new Vector3(90f, 0, 0), 4f, false, 5f, 2, 0);
        if (isChase == false)
        {
            timer = spawntimer;
            escapeTimer = 0;
            GameController.instance.ChangeMusic(music);
        }
        else
            timer = spawntimer - 2;

        isChase = true;

    }


    private void SetDestination()
    {
      if (!Escaped || PlayerDistance < 7)
      {
            _navMeshagent.SetDestination(Player.transform.position);
      }

    }

    void Path()
    {
        if (Vector3.Distance(new Vector3(ActualPath[currentNode].position.x, transform.position.y, ActualPath[currentNode].position.z), transform.position) < Distance)
        {
            if (currentNode != ActualPath.Length-1)
                currentNode += 1;
            Debug.Log("Nodo " + currentNode + " de " + ActualPath.Length);
        }

        Vector3 Point = new Vector3(ActualPath[currentNode].position.x, transform.position.y, ActualPath[currentNode].position.z) - transform.position;

        toAngle = Quaternion.LookRotation(Point);

        transform.position += (transform.forward * speed) * Time.deltaTime;
        transform.rotation = Quaternion.Lerp(transform.rotation, toAngle, 4f * Time.deltaTime);

        if ((Vector3.Distance(new Vector3(ActualPath[currentNode].position.x, transform.position.y, ActualPath[currentNode].position.z), transform.position) < Distance) && currentNode == ActualPath.Length-1)
        {
            isPath = false;
            Debug.Log("Path Terminado");
        }
    }

    public void EndPath()
    {
        isPath = false;
    }

    public void SetPath(Transform[] path)
    {
        Debug.Log("Iniciando Path");
        currentNode = 0;
        ActualPath = path;
        isPath = true;
    }


    public override void Event_Spawn(bool instant, Vector3 here)
    {
        col.enabled = false;
        isActive = true;
        isSpawn = true;
        anim.SetBool("move", false);
        _navMeshagent.enabled = false;
        if (!instant)
        {
            timer = spawntimer;
            anim.SetTrigger("spawn");
            isSpawn = false;
        }
        
        transform.position = here;
        isEvent = true;
        
        sfx.PlayOneShot(Sfx[0]);
        playedHorror = false;
        escapeTimer = 20;

        isChase = true;

    }

    public override void StopEvent()
    {
        isEvent = false;
        col.enabled = true;
        GameController.instance.ChangeMusic(music);
    }








    private void OnTriggerStay(Collider other)
    {
        if ((isSpawn) && (other.gameObject.CompareTag("Player")))
        {
            other.gameObject.GetComponent<Player_Control>().Death(2);
            UnSpawn();
            Debug.Log("You are ded ded ded");
        }

    }


}