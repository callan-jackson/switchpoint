import type { RouteObject } from 'react-router'
import { AppLayout } from './AppLayout'
import { RequireAuth } from './RequireAuth'
import { NotFound, RouteError } from './RouteError'

/**
 * Route table for the data router.
 *
 * Every page is code-split with `lazy`, so the initial bundle carries only the shell, the sign-in
 * screen and the API client. `handle.crumb` supplies the breadcrumb label for segments that are
 * ids. `/login` sits outside the layout; everything else is behind `RequireAuth`.
 */
export const routes: RouteObject[] = [
  {
    path: '/login',
    lazy: async () => ({ Component: (await import('@/features/auth/LoginPage')).default }),
  },
  {
    element: <RequireAuth />,
    children: [
      {
        path: '/',
        element: <AppLayout />,
        errorElement: <RouteError />,
        children: [
          {
            index: true,
            lazy: async () => ({ Component: (await import('@/pages/Dashboard/DashboardPage')).default }),
          },
          {
            path: 'clients',
            handle: { crumb: 'Clients' },
            children: [
              {
                index: true,
                lazy: async () => ({ Component: (await import('@/pages/Clients/ClientsPage')).default }),
              },
              {
                path: ':clientId',
                handle: { crumb: 'Client' },
                lazy: async () => ({
                  Component: (await import('@/pages/Clients/ClientDetailPage')).default,
                }),
              },
            ],
          },
          {
            path: 'pension-switch',
            handle: { crumb: 'Pension Switch' },
            children: [
              {
                index: true,
                lazy: async () => ({
                  Component: (await import('@/pages/PensionSwitch/PensionSwitchPage')).default,
                }),
              },
              {
                path: ':analysisId',
                handle: { crumb: 'Analysis' },
                lazy: async () => ({
                  Component: (await import('@/pages/PensionSwitch/PensionSwitchPage')).default,
                }),
              },
            ],
          },
          {
            path: 'db-transfer',
            handle: { crumb: 'DB Transfer' },
            children: [
              {
                index: true,
                lazy: async () => ({
                  Component: (await import('@/pages/DbTransfer/DbTransferPage')).default,
                }),
              },
              {
                path: ':analysisId',
                handle: { crumb: 'Analysis' },
                lazy: async () => ({
                  Component: (await import('@/pages/DbTransfer/DbTransferPage')).default,
                }),
              },
            ],
          },
          {
            path: 'cashflow',
            handle: { crumb: 'Cashflow' },
            children: [
              {
                index: true,
                lazy: async () => ({ Component: (await import('@/pages/Cashflow/CashflowPage')).default }),
              },
              {
                path: ':planId',
                handle: { crumb: 'Plan' },
                lazy: async () => ({ Component: (await import('@/pages/Cashflow/CashflowPage')).default }),
              },
            ],
          },
          {
            path: 'funds',
            handle: { crumb: 'Fund Research' },
            lazy: async () => ({
              Component: (await import('@/pages/FundResearch/FundResearchPage')).default,
            }),
          },
          {
            path: 'products',
            handle: { crumb: 'Products & Charges' },
            children: [
              {
                index: true,
                lazy: async () => ({ Component: (await import('@/pages/Products/ProductsPage')).default }),
              },
              {
                path: ':productId',
                handle: { crumb: 'Product' },
                lazy: async () => ({
                  Component: (await import('@/pages/Products/ProductDetailPage')).default,
                }),
              },
            ],
          },
          {
            path: 'reports',
            handle: { crumb: 'Reports' },
            lazy: async () => ({ Component: (await import('@/pages/Reports/ReportsPage')).default }),
          },
          {
            path: 'audit',
            handle: { crumb: 'Audit' },
            lazy: async () => ({ Component: (await import('@/pages/Audit/AuditPage')).default }),
          },
          {
            path: 'settings',
            handle: { crumb: 'Settings' },
            lazy: async () => ({ Component: (await import('@/pages/Settings/SettingsPage')).default }),
          },
          { path: '*', element: <NotFound /> },
        ],
      },
    ],
  },
]
