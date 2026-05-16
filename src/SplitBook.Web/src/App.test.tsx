import { render, screen } from '@testing-library/react';
import App from './App';

describe('App', () => {
  test('displays a heading containing "SplitBook" on the home route', () => {
    render(<App />);
    const heading = screen.getByRole('heading', { name: /SplitBook/i });
    expect(heading).toBeInTheDocument();
  });
});
