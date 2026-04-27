const API_BASE_URL = 'http://localhost:5157';

export const chatService = {
  async *streamChat(message) {
    try {
      const response = await fetch(`${API_BASE_URL}/api/hragent/chat`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ message }),
      });

      if (!response.ok) {
        throw new Error(`API error: ${response.statusText}`);
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';

      while (true) {
        const { done, value } = await reader.read();

        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');

        // Keep the last incomplete line in the buffer
        buffer = lines.pop() || '';

        for (const line of lines) {
          if (line.trim()) {
            yield line;
          }
        }
      }

      // Process any remaining data
      if (buffer.trim()) {
        yield buffer;
      }
    } catch (error) {
      console.error('Chat error:', error);
      throw error;
    }
  },
};
