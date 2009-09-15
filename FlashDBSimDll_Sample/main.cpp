#include "stdafx.h"
#include <ctime>
#include "TrivalBlockDevice.h"
#include "TrivalBufferManager.h"
#include "LRUBufferManager.h"
#include "CFLRUBufferManager.h"
#include "LRUWSRBufferManager.h"
using namespace std;
using namespace std::tr1;

void main()
{
	int bufferSize = 1000;

	shared_ptr<IBlockDevice> pdev(new TrivalBlockDevice(2048));
	shared_ptr<IBlockDevice> pdevCFLRU(new TrivalBlockDevice(2048));
	shared_ptr<IBlockDevice> pdevLRUWSR(new TrivalBlockDevice(2048));
	//shared_ptr<IBufferManager> pmgr(new TrivalBufferManager(pdev));
	shared_ptr<IBufferManager> pmgr(new LRUBufferManager(pdev, bufferSize));
	shared_ptr<IBufferManager> pmgrCFLRU(new CFLRUBufferManager(pdevCFLRU, bufferSize, bufferSize*7/8));
	shared_ptr<IBufferManager> pmgrLRUWSR(new LRUWSRBufferManager(pdevLRUWSR, bufferSize));

	int fcount = 0;
	//srand(clock());

	while (fcount++ < 10000)
	{

		size_t addr = rand()/10;
		int rw = rand() % 2;
		char buf[2048];

		//cout<<addr<<","<<rw<<endl;

		if (rw == 0)
		{
			pmgr->Read(addr, buf);
			pmgrCFLRU->Read(addr, buf);
			pmgrLRUWSR->Read(addr, buf);
		}
		else{
			pmgr->Write(addr, buf);
			pmgrCFLRU->Write(addr, buf);
			pmgrLRUWSR->Write(addr, buf);
		}
	}

	pmgr->Flush();
	pmgrCFLRU->Flush();
	pmgrLRUWSR->Flush();

	cout<< "LRU" <<endl
		<< pmgr->GetReadCount() << endl
		<< pmgr->GetWriteCount() << endl
		<< pdev->GetReadCount() << endl
		<< pdev->GetWriteCount() << endl
		<< pdev->GetTotalCost() << endl
		<< endl;

	cout<< "CFLRU" <<endl
		<< pmgrCFLRU->GetReadCount() << endl
		<< pmgrCFLRU->GetWriteCount() << endl
		<< pdevCFLRU->GetReadCount() << endl
		<< pdevCFLRU->GetWriteCount() << endl
		<< pdevCFLRU->GetTotalCost() << endl
		<< endl;

	cout<< "LRUWSR" <<endl
		<< pmgrLRUWSR->GetReadCount() << endl
		<< pmgrLRUWSR->GetWriteCount() << endl
		<< pdevLRUWSR->GetReadCount() << endl
		<< pdevLRUWSR->GetWriteCount() << endl
		<< pdevLRUWSR->GetTotalCost() << endl
		<<endl;
}