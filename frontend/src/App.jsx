import { useState, useRef, useEffect } from 'react'
import { chatService } from './services/chatService'
import { useToast } from './contexts/ToastContext'
import './App.css'

function App() {
  const [messages, setMessages] = useState([])
  const [inputValue, setInputValue] = useState('')
  const [isLoading, setIsLoading] = useState(false)
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
    
    if (!inputValue.trim()) {
      addToast('Please enter a message', 'warning')
      return
    }

    // Add user message
    const userMessage = { type: 'user', content: inputValue }
    const userInput = inputValue
    setMessages(prev => [...prev, userMessage])
    setInputValue('')
    setIsLoading(true)

    try {
      // Create a placeholder for assistant message
      const assistantMessage = { type: 'assistant', content: '' }
      setMessages(prev => [...prev, assistantMessage])

      // Stream the response
      for await (const chunk of chatService.streamChat(userInput)) {
        setMessages(prev => {
          const newMessages = [...prev]
          newMessages[newMessages.length - 1].content += chunk
          return newMessages
        })
      }
      addToast('Response received successfully', 'success')
    } catch (err) {
      addToast(`Error: ${err.message}`, 'error')
      setMessages(prev => prev.slice(0, -1)) // Remove the assistant message on error
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="chat-container">
      <div className="chat-box">
        <header className="chat-header">
          <h1>HR Policy Agent</h1>
          <p>Get clear, compliant answers about HR policies in real time.</p>
        </header>

        <div className="messages-container">
        {messages.length === 0 && (
          <div className="empty-state">
            <h2>Welcome to HR Policy Agent</h2>
            <p>Ask me any questions about HR policies and guidelines.</p>
          </div>
        )}

        {messages.map((msg, idx) => (
          <div key={idx} className={`message ${msg.type}`}>
            <div className="message-content">
              {msg.content}
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
      </div>
    </div>
  )
}

export default App
