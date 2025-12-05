#pragma once

#include <windows.h>
#include <unknwn.h>
#include <shlobj.h>
#include <shlwapi.h>

// Forward declaration
class CSortBySchlongExtension;

/// <summary>
/// Class factory for creating CSortBySchlongExtension instances.
/// </summary>
class CClassFactory : public IClassFactory
{
public:
    CClassFactory();
    virtual ~CClassFactory();

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    // IClassFactory
    IFACEMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv) override;
    IFACEMETHODIMP LockServer(BOOL fLock) override;

    // Global reference counting for DllCanUnloadNow
    static LONG GetObjectCount() { return s_cObjects; }
    static LONG GetLockCount() { return s_cLocks; }

private:
    LONG m_cRef;
    static LONG s_cObjects;
    static LONG s_cLocks;
};

