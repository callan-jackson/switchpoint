import { RouterProvider, createBrowserRouter } from 'react-router'
import { Providers } from '@/app/Providers'
import { routes } from '@/app/routes'

const router = createBrowserRouter(routes)

export default function App() {
  return (
    <Providers>
      <RouterProvider router={router} />
    </Providers>
  )
}
