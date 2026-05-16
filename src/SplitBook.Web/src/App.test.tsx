import { render, screen } from '@testing-library/react';
import App from './App';

describe('App', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  test('displays a heading containing "SplitBook" on the home route', () => {
    localStorage.setItem('splitbook_token', 'fake-jwt-token');
    render(<App />);
    const heading = screen.getByRole('heading', { name: /SplitBook/i });
    expect(heading).toBeInTheDocument();
  });
});
