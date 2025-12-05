#include "ClassFactory.h"
#include "SortBySchlongExtension.h"
#include <new>

#pragma comment(lib, "shlwapi.lib")

LONG CClassFactory::s_cObjects = 0;
LONG CClassFactory::s_cLocks = 0;

CClassFactory::CClassFactory() : m_cRef(1)
{
    InterlockedIncrement(&s_cObjects);
}

CClassFactory::~CClassFactory()
{
    InterlockedDecrement(&s_cObjects);
}

IFACEMETHODIMP CClassFactory::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] =
    {
        QITABENT(CClassFactory, IClassFactory),
        { nullptr, 0 }
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP_(ULONG) CClassFactory::AddRef()
{
    return InterlockedIncrement(&m_cRef);
}

IFACEMETHODIMP_(ULONG) CClassFactory::Release()
{
    ULONG cRef = InterlockedDecrement(&m_cRef);
    if (cRef == 0)
    {
        delete this;
    }
    return cRef;
}

IFACEMETHODIMP CClassFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv)
{
    *ppv = nullptr;

    // Aggregation is not supported
    if (pUnkOuter != nullptr)
    {
        return CLASS_E_NOAGGREGATION;
    }

    // Create the extension instance
    CSortBySchlongExtension* pExt = new (std::nothrow) CSortBySchlongExtension();
    if (pExt == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    // Query for the requested interface
    HRESULT hr = pExt->QueryInterface(riid, ppv);
    pExt->Release();

    return hr;
}

IFACEMETHODIMP CClassFactory::LockServer(BOOL fLock)
{
    if (fLock)
    {
        InterlockedIncrement(&s_cLocks);
    }
    else
    {
        InterlockedDecrement(&s_cLocks);
    }
    return S_OK;
}

