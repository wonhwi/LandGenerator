# LandGenerator
비효율적 리소스 낭비를 줄일수 있도록 만든 데이터 기반 타일링 오브젝트들을 배치하는 툴


https://github.com/user-attachments/assets/2f1f7724-f714-481c-89fc-b74ce72e321b


### 맵 생성 씬

맵 생성 씬에서 사용되는 스크립트 인스펙터 사진입니다

<img width="750" height="437" alt="Image" src="https://github.com/user-attachments/assets/cf5c7a66-b932-4fff-aba7-4a5f3c49f615" />

1. City Type - 작업 할 City 목록 (다중 선택 가능)
2. MeshBakeType - 같은 재질 묶는 기능까지 추가 하였으나, 수작업으로 최적화하는 방식으로 결정되어 None으로 설정
3. LandDataPath - 해당 랜드 맵 데이터가 있는 경로

### Data Map Scriptable Object

맵 생성 씬, NavMesh Bake 시 상요되는 Scripable Object 인스펙터 사진입니다

<img width="748" height="1106" alt="Image" src="https://github.com/user-attachments/assets/8038b6e7-10c5-4e71-bb7f-82e8460580e0" />

1. 자동 NavMeshBaker를 위한 프리팹 네이밍 별 타입들을 분리 및 관리 하는 데이터 입니다.

### NavMeshBaker Script

맵 생성 후 NavMeshBaker를 진행하는 스크립트 인스펙터 사진입니다.

<img width="708" height="613" alt="Image" src="https://github.com/user-attachments/assets/cfada5b6-5395-4930-9dfc-3c0e3db7362a" />

1. DataMap의 타입별로 어떤 NavMeshModifer를 사용할지 선언해주고 프리팹들이 각각의 타입에 따라 자식으로 넣은 후, 최종 NavMeshBaker에 반영되게 하였습니다.
