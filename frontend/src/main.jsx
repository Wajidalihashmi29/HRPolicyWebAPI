import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { ToastProvider } from './contexts/ToastContext'
import { ToastContainer } from './components/ToastContainer'
import './index.css'
import App from './App.jsx'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <ToastProvider>
      <App />
      <ToastContainer />
    </ToastProvider>
  </StrictMode>,
)
