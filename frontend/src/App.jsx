import { useState, useRef, useEffect } from 'react'
import { chatService } from './services/chatService'
import { useToast } from './contexts/ToastContext'
import './App.css'

function CitationBlock({ citations }) {
  const [open, setOpen] = useState(false)
  if (!citations || citations.length === 0) return null

  return (
    <div className="citations">
      <button className="citations-toggle" onClick={() => setOpen(!open)}>
        {open ? '▾' : '▸'} {citations.length} source{citations.length > 1 ? 's' : ''}
      </button>
      {open && (
        <div className="citations-list">
          {citations.map((c, i) => {
            // Parse structured citation format from CitationFormatter.cs
            const lines = c.split('\n').map(l => l.trim()).filter(Boolean)
            const get = (prefix) => {
              const line = lines.find(l => l.startsWith(prefix))
              return line ? line.slice(prefix.length).trim() : null
            }
            const doc = get('Document:')
            const page = get('Page:')
            const section = get('Section:')
            const text = get('Text:')
            const metaParts = [section && `Section: ${section}`, page && `Page: ${page}`].filter(Boolean)
            return (
              <div key={i} className="citation-item">
                <span className="citation-num">{i + 1}</span>
                <div className="citation-body">
                  {doc && (
                    <div className="citation-doc">
                      📄 {doc}
                    </div>
                  )}
                  {metaParts.length > 0 && (
                    <div className="citation-meta">
                      {metaParts.join(' • ')}
                    </div>
                  )}
                  {text && (
                    <div className="citation-text">
                      {text}
                    </div>
                  )}
                  {!page && !section && (
                    <div className="citation-meta citation-missing">No page/section metadata available</div>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}

function App() {
  const [messages, setMessages] = useState([])
  const [inputValue, setInputValue] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const assistantIndexRef = useRef(null)
  const messagesEndRef = useRef(null)
  const { addToast } = useToast()

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }

  useEffect(() => {
    scrollToBottom()
  }, [messages])

  const handleSendMessage = async (e) => {
    e.preventDefault()

    console.log('App: handleSendMessage called', inputValue)

    if (!inputValue.trim()) {
      addToast('Please enter a message', 'warning')
      return
    }

    const userInput = inputValue
    setMessages(prev => {
      const next = [
        ...prev,
        { type: 'user', content: userInput, citations: [] },
        { type: 'assistant', content: '', citations: [] },
      ]
      assistantIndexRef.current = next.length - 1
      return next
    })
    setInputValue('')
    setIsLoading(true)

    try {
      const pendingCitations = []

      for await (const rawEvent of chatService.streamChat(userInput)) {
        console.log('App: rawEvent received', rawEvent)
        const event =
          typeof rawEvent === 'string'
            ? { type: 'text', text: rawEvent }
            : rawEvent

        console.log('chat stream event', event)

        if (event.type === 'done') {
          break
        }

        if (event.type === 'error') {
          throw new Error(event.text ?? 'Unknown chat error')
        }

        if (event.type === 'text') {
          setMessages(prev => {
            const updated = [...prev]
            const targetIndex = assistantIndexRef.current ?? updated.length - 1
            const target = updated[targetIndex] ?? updated[updated.length - 1]
            if (!target) return updated

            updated[targetIndex] = {
              ...target,
              content: (target.content ?? '') + event.text,
            }
            return updated
          })
          await new Promise(resolve => setTimeout(resolve, 0))
        } else if (event.type === 'citation') {
          setMessages(prev => {
            const updated = [...prev]
            const targetIndex = assistantIndexRef.current ?? updated.length - 1
            const target = updated[targetIndex] ?? updated[updated.length - 1]
            if (!target) return updated

          updated[targetIndex] = {
            ...target,
            citations: [...(target.citations ?? []), ...pendingCitations],
          }
          return updated
        })
      }

      addToast('Response received successfully', 'success')
    } catch (err) {
      addToast(`Error: ${err.message}`, 'error')
      setMessages(prev => prev.slice(0, -1))
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="chat-container">
      <div className="chat-box">
        <header className="chat-header">
          <h1>Global Edge Corporations - HR Help Desk</h1>
          <p>Get clear, compliant answers about HR policies in real time.</p>
        </header>

        <div className="messages-container">
          {messages.length === 0 && (
            <div className="empty-state">
              <h2>Welcome to HR Help Desk</h2>
              <p>Ask any questions about HR policies and guidelines.</p>
            </div>
          )}

          {messages.map((msg, idx) => (
            <div key={idx} className={`message ${msg.type}`}>
              <div className="message-content">
                {msg.content}
                {msg.type === 'assistant' && msg.citations.length > 0 && (
                  <CitationBlock citations={msg.citations} />
                )}
                {msg.type === 'assistant' && isLoading && idx === messages.length - 1 && (
                  <span className="typing-indicator">
                    <span></span><span></span><span></span>
                  </span>
                )}
              </div>
            </div>
          ))}

          <div ref={messagesEndRef} />
        </div>

        <form className="input-form" onSubmit={handleSendMessage}>
          <input
            type="text"
            value={inputValue}
            onChange={(e) => setInputValue(e.target.value)}
            placeholder="Ask about HR policies..."
            disabled={isLoading}
            className="message-input"
          />
          <button type="submit" disabled={isLoading} className="send-button">
            {isLoading ? 'Sending...' : 'Send'}
          </button>
        </form>
        <div className="chat-warning">
          AI generated content may be incorrect. Please verify before relying on it.
        </div>
      </div>
    </div>
  )
}

export default App