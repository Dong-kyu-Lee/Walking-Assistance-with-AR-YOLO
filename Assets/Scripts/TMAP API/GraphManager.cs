using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class Edge
{
    public Node targetNode; // 이 길이 향하는 목적지 교차로
    public float weight;    // 길의 길이 (비용, 거리)

    public Edge(Node target, float weight)
    {
        this.targetNode = target;
        this.weight = weight;
    }
}

// 2. 교차로(Node)를 정의하는 클래스
public class Node
{
    public string id;              // 교차로 이름 (예: "정문", "공학관_앞")
    public Vector3 position;       // 유니티 3D 공간상의 좌표 (또는 변환된 GPS 좌표)
    public List<Edge> edges;       // 이 교차로와 연결된 다른 길들의 목록

    public Node(string id, Vector3 pos)
    {
        this.id = id;
        this.position = pos;
        this.edges = new List<Edge>();
    }

    // 양방향 길을 연결하는 편의 함수 (거리는 Vector3로 자동 계산!)
    public void AddEdge(Node targetNode)
    {
        // 두 교차로 사이의 실제 물리적 거리를 가중치(비용)로 자동 계산
        float distance = Vector3.Distance(this.position, targetNode.position);

        this.edges.Add(new Edge(targetNode, distance));
        targetNode.edges.Add(new Edge(this, distance)); // 보통 길은 양방향이니까!
    }
}



public class GraphManager : MonoBehaviour
{
    public Dictionary<string, Node> nodes = new Dictionary<string, Node>();

    void Start()
    {
        BuildCampusGraph();
    }

    void BuildCampusGraph()
    {
        // 1. 교차로(Node) 생성 및 좌표 입력 (임의의 로컬 좌표 예시)
        Node gate = new Node("정문", new Vector3(0, 0, 0));
        Node library = new Node("도서관", new Vector3(0, 0, 50));
        Node engineering = new Node("공학관", new Vector3(30, 0, 50));
        Node cafeteria = new Node("학생식당", new Vector3(30, 0, 100));

        // 2. 생성한 노드를 딕셔너리에 등록
        nodes.Add(gate.id, gate);
        nodes.Add(library.id, library);
        nodes.Add(engineering.id, engineering);
        nodes.Add(cafeteria.id, cafeteria);

        // 3. 교차로끼리 연결 (Link 생성)
        gate.AddEdge(library);         // 정문 <-> 도서관 연결
        library.AddEdge(engineering);  // 도서관 <-> 공학관 연결
        engineering.AddEdge(cafeteria);// 공학관 <-> 학생식당 연결

        Debug.Log("캠퍼스 그래프 생성 완료! 총 노드 수: " + nodes.Count);
    }
}
