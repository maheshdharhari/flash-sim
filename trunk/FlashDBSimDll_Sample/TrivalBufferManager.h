#ifndef _TRIVAL_BUFFER_MANAGER_H_
#define _TRIVAL_BUFFER_MANAGER_H_

#include <memory>
#include "BufferManagerBase.h"


class TrivalBufferManager : public BufferManagerBase
{
public:
	TrivalBufferManager(std::tr1::shared_ptr<class IBlockDevice> pDevice);

protected:
	void DoRead(size_t addr, void *result);
	void DoWrite(size_t addr, const void *data);
	void DoFlush();

private:
	std::tr1::shared_ptr<class IBlockDevice> pdev_;
};

#endif
